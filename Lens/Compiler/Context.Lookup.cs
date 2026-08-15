using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Lens.Compiler.Entities;
using Lens.Resolver;
using Lens.SyntaxTree.Declarations.Functions;
using Lens.Translations;

namespace Lens.Compiler
{
    internal partial class Context
    {
        /// <summary>
        /// Finds a locally declared type.
        /// </summary>
        public TypeEntity FindType(string name)
        {
            _definedTypes.TryGetValue(name, out TypeEntity declared);
            return declared;
        }

        /// <summary>
        /// Resolves a type by its string signature.
        /// Warning: this method might return a TypeBuilder as well as a Type, if the signature points to an inner type.
        /// </summary>
        public TypeEntry ResolveType(string signature)
        {
            return ResolveType(TypeSignature.Parse(signature));
        }

        /// <summary>
        /// Resolves a type by its signature, recording the reference when anything is watching.
        /// </summary>
        public TypeEntry ResolveType(TypeSignature signature, bool allowUnspecified = false)
        {
            var result = ResolveTypeCore(signature, allowUnspecified);

            if (TrackTypeReferences)
                RecordTypeReference(signature, result);

            return result;
        }

        /// <summary>
        /// Resolves a type by its signature.
        /// </summary>
        private TypeEntry ResolveTypeCore(TypeSignature signature, bool allowUnspecified)
        {
            if (allowUnspecified && signature.FullSignature == "_")
                return null;

            // a generic parameter of the enclosing declaration wins over anything else
            if (signature.Arguments == null && string.IsNullOrEmpty(signature.Postfix))
            {
                var typeParam = Resolver.FindTypeParameter(signature.Name);
                // no Builder check: the parameter's entry answers from its constraint model, so a
                // signature naming T resolves whether or not the declaration has been emitted
                if (typeParam != null)
                    return typeParam.TypeInfo;
            }

            var declared = FindType(signature.FullSignature);
            if (declared != null)
            {
                if (declared.GenericParameterCount > 0)
                    Error(signature, CompilerMessages.GenericTypeArgCountMismatch, declared.Name, declared.GenericParameterCount, 0);

                return declared.TypeInfo;
            }

            // a locally declared generic type: the arity-mangled name is only used in the assembly,
            // so the signature is matched against the LENS name and instantiated here
            if (signature.Arguments != null && string.IsNullOrEmpty(signature.Postfix))
            {
                var genericDeclared = FindType(signature.Name);
                if (genericDeclared != null)
                {
                    if (genericDeclared.GenericParameterCount != signature.Arguments.Length)
                        Error(signature, CompilerMessages.GenericTypeArgCountMismatch, genericDeclared.Name, genericDeclared.GenericParameterCount, signature.Arguments.Length);

                    var args = signature.Arguments.Select(x => ResolveType(x)).ToArray();
                    return GenericHelper.MakeGenericTypeChecked(Resolver, genericDeclared.TypeInfo, args);
                }
            }

            return _typeResolver.ResolveType(signature);
        }

        /// <summary>
        /// Resolves the type a pattern names, substituting the type arguments of the value being
        /// matched into it.
        ///
        /// A pattern spells only the name of a record or of a label ("Some", "Pair"), so the
        /// arguments of a generic one are taken from the scrutinee: a label of a generic algebraic
        /// type is generic in exactly the parameters of the type itself, in the same order.
        /// </summary>
        public TypeEntry ResolvePatternType(TypeEntity entity, TypeEntry expressionType)
        {
            if (!entity.IsGeneric)
                return entity.TypeInfo;

            var arguments = FindTypeArguments(expressionType, entity.GenericParameterCount);
            if (arguments == null)
                Error(CompilerMessages.GenericTypeArgCountMismatch, entity.Name, entity.GenericParameterCount, 0);

            // in the entry model rather than through the builders: the declaration has no builder
            // until the assembly is emitted, and an argument taken from the scrutinee may itself be
            // a parameter of the enclosing function, which has none either
            return GenericHelper.MakeGenericTypeChecked(Resolver, entity.TypeInfo, arguments);
        }

        /// <summary>
        /// Looks for the type arguments of the matched value, walking up its inheritance chain.
        /// </summary>
        private static TypeEntry[] FindTypeArguments(TypeEntry type, int count)
        {
            var curr = type;
            while (curr != null)
            {
                if (curr.IsGenericType)
                {
                    var args = curr.GenericArguments;
                    if (args.Length == count)
                        return args;
                }

                // no NotSupportedException guard: an entry answers about its base type from the
                // model, so the walk cannot be aborted by reflection refusing to answer any more
                curr = curr.BaseType;
            }

            return null;
        }

        /// <summary>
        /// Resolves a field from a type by its name, including declared types.
        /// </summary>
        public FieldWrapper ResolveField(TypeEntry type, string name)
        {
            var declared = FindDeclaredType(type);
            if (declared == null)
                return ResolveHostField(type, name);

            var fi = declared.Entity.ResolveField(name);
            return new FieldWrapper
            {
                Name = name,
                DeclaringType = type,

                FieldInfoSource = () => declared.MemberOf(fi.FieldBuilder),
                IsStatic = fi.IsStatic,

                // the resolved type of the field, not the builder's: they are the same type, and
                // only one of the two can be asked before the field has been emitted
                FieldType = declared.Substitute(fi.Type)
            };
        }

        /// <summary>
        /// Resolves a property from a type by its name.
        /// </summary>
        public PropertyWrapper ResolveProperty(TypeEntry type, string name)
        {
            if (FindDeclaredType(type) == null)
                return ResolveHostProperty(type, name);

            // no internal properties
            throw new KeyNotFoundException();
        }

        /// <summary>
        /// Resolves an event from a type by its name.
        /// </summary>
        public EventWrapper ResolveEvent(TypeEntry type, string name)
        {
            if (FindDeclaredType(type) == null)
                return ResolveHostEvent(type, name);

            // no internal events
            throw new KeyNotFoundException();
        }

        /// <summary>
        /// Resolves a constructor from a type by the list of arguments.
        /// </summary>
        public ConstructorWrapper ResolveConstructor(TypeEntry type, TypeEntry[] argTypes)
        {
            var declared = FindDeclaredType(type);
            if (declared == null)
                return ResolveHostConstructor(type, argTypes);

            var ctor = declared.Entity.ResolveConstructor(argTypes, declared.Instantiation);

            return new ConstructorWrapper
            {
                DeclaringType = type,
                ConstructorInfoSource = () => declared.MemberOf(ctor.ConstructorBuilder),
                ArgumentTypes = ctor.GetArgumentTypes(this).Select(declared.Substitute).ToArray(),

                IsPartiallyApplied = ReflectionHelper.IsPartiallyApplied(argTypes),
                IsVariadic = false // built-in ctors can't do that
            };
        }

        /// <summary>
        /// Resolves a method by its name and argument types. If generic arguments are passed, they are also applied.
        /// Generic arguments whose values can be inferred from argument types can be skipped.
        /// </summary>
        public MethodWrapper ResolveMethod(TypeEntry type, string name, TypeEntry[] argTypes, TypeEntry[] hints = null, LambdaResolver resolver = null)
        {
            // only the members of a parameter's constraints are available on it
            if (type.IsGenericParameter)
            {
                foreach (var constraint in ResolveConstraintsOf(type))
                {
                    try
                    {
                        return ResolveMethod(constraint, name, argTypes, hints, resolver);
                    }
                    catch (KeyNotFoundException)
                    {
                    }
                }

                throw new KeyNotFoundException();
            }

            var declared = FindDeclaredType(type);
            if (declared == null)
                return ResolveHostMethod(type, name, argTypes, hints, resolver);

            try
            {
                var method = declared.Entity.ResolveMethod(name, argTypes, instantiation: declared.Instantiation);
                var mw = WrapMethod(declared, method, ReflectionHelper.IsPartiallyApplied(argTypes));

                if (method.IsImported && method.MethodInfo.IsGenericMethod)
                {
                    var argTypeDefs = method.MethodInfo.GetParameters().Select(p => p.ParameterType).ToArray();
                    var genericDefs = method.MethodInfo.GetGenericArguments();
                    var genericValues = GenericHelper.ResolveMethodGenericsByArgs(Resolver, argTypeDefs, TypeEntry.Materialize(argTypes), genericDefs, TypeEntry.Materialize(hints), resolver);

                    mw.MethodInfo = method.MethodInfo.MakeGenericMethod(genericValues);
                    mw.ArgumentTypes = method.GetArgumentTypes(this).Select(t => TypeEntryCache.Of(GenericHelper.ApplyGenericArguments(t.Materialize(), genericDefs, genericValues))).ToArray();
                    mw.GenericArguments = genericValues.Select(TypeEntryCache.Of).ToArray();
                    mw.ReturnType = TypeEntryCache.Of(GenericHelper.ApplyGenericArguments(method.MethodInfo.ReturnType, genericDefs, genericValues));
                }
                else if (method.IsGeneric)
                {
                    InstantiateGenericMethod(mw, method, argTypes, hints, resolver);
                }
                else
                {
                    if (hints != null)
                        Error(CompilerMessages.GenericArgsToNonGenericMethod, name);
                }

                CheckFixedParameters(mw, argTypes);

                return mw;
            }
            catch (KeyNotFoundException)
            {
                return ResolveMethod(declared.BaseType, name, argTypes, hints, resolver);
            }
        }

        /// <summary>
        /// Infers the generic arguments of a LENS-declared generic function from the call site
        /// and rewrites the wrapper to point at the instantiated method.
        /// </summary>
        private void InstantiateGenericMethod(MethodWrapper mw, MethodEntity method, TypeEntry[] argTypes, TypeEntry[] hints, LambdaResolver resolver)
        {
            // there is no assembly, so the parameters have no builders and the type-based resolver
            // has nothing to work with: infer from the declared entries instead
            if (method.GenericParameters[0].Builder == null)
            {
                InferGenericMethod(mw, method, argTypes, hints);
                return;
            }

            var genericDefs = method.GenericParameters.Select(p => (Type) p.Builder).ToArray();

            if (hints != null && hints.Length != genericDefs.Length)
                Error(CompilerMessages.GenericArgCountMismatch);

            var argTypeDefs = method.GetArgumentTypes(this);
            var genericValues = GenericHelper.ResolveMethodGenericsByArgs(Resolver, TypeEntry.Materialize(argTypeDefs), TypeEntry.Materialize(argTypes), genericDefs, TypeEntry.Materialize(hints), resolver);

            GenericHelper.CheckConstraints(Resolver, method.GenericParameters, genericValues);

            mw.MethodInfo = method.MethodBuilder.MakeGenericMethod(genericValues);
            mw.ArgumentTypes = argTypeDefs.Select(t => TypeEntryCache.Of(GenericHelper.ApplyGenericArguments(t.Materialize(), genericDefs, genericValues))).ToArray();
            mw.GenericArguments = genericValues.Select(TypeEntryCache.Of).ToArray();
            mw.ReturnType = TypeEntryCache.Of(GenericHelper.ApplyGenericArguments(method.ReturnType.Materialize(), genericDefs, genericValues));
        }

        /// <summary>
        /// Infers the generic arguments of a LENS-declared generic function from the call site while
        /// no assembly exists, and rewrites the wrapper into their terms.
        ///
        /// The signatures that reach here are the ones that can be resolved before emission: a naked
        /// type parameter, or an array of one. A composite signature such as Option&lt;T&gt; is still
        /// spelled in terms of the parameter builders and cannot be resolved at all until they exist,
        /// which is the remaining coupling between binding and emission.
        ///
        /// There is no MethodInfo to hand out: only emission needs one, and only emission has one.
        /// </summary>
        private void InferGenericMethod(MethodWrapper mw, MethodEntity method, TypeEntry[] argTypes, TypeEntry[] hints)
        {
            var parameters = method.GenericParameters;

            if (hints != null && hints.Length != parameters.Count)
                Error(CompilerMessages.GenericArgCountMismatch);

            var values = new TypeEntry[parameters.Count];
            if (hints != null)
                for (var idx = 0; idx < hints.Length; idx++)
                    values[idx] = hints[idx];

            var declaredArgs = method.GetArgumentTypes(this);
            var count = Math.Min(declaredArgs.Length, argTypes.Length);
            for (var idx = 0; idx < count; idx++)
                InferGenericArgument(declaredArgs[idx], argTypes[idx], parameters, values);

            for (var idx = 0; idx < values.Length; idx++)
                if (values[idx] == null)
                    Error(CompilerMessages.GenericArgumentNotResolved, parameters[idx].Name);

            GenericHelper.CheckConstraints(Resolver, parameters, values);

            mw.MethodInfo = null;
            mw.ArgumentTypes = declaredArgs.Select(x => SubstituteGenericArguments(x, parameters, values)).ToArray();
            mw.GenericArguments = values;
            mw.ReturnType = SubstituteGenericArguments(method.ReturnType, parameters, values);
        }

        /// <summary>
        /// Reads the value of a type parameter off one argument of the call site.
        /// </summary>
        private static void InferGenericArgument(TypeEntry declared, TypeEntry actual, IList<GenericParameterEntity> parameters, TypeEntry[] values)
        {
            var entity = (declared as GenericParameterEntry)?.Entity;
            if (entity != null)
            {
                var idx = parameters.IndexOf(entity);
                if (idx >= 0 && values[idx] == null)
                    values[idx] = actual;

                return;
            }

            if (declared.IsArray && !ReferenceEquals(actual, null) && actual.IsArray)
                InferGenericArgument(declared.ElementType, actual.ElementType, parameters, values);
        }

        /// <summary>
        /// Rewrites a declared type in the terms of the inferred type arguments.
        /// </summary>
        private TypeEntry SubstituteGenericArguments(TypeEntry type, IList<GenericParameterEntity> parameters, TypeEntry[] values)
        {
            // the shared substitution walks nested instantiations and by-ref types as well as
            // arrays, so a declared signature mentioning List<T> substitutes as readily as T[]
            return ConstructedTypeEntry.SubstituteInto(
                Resolver,
                type,
                parameters.Select(x => x.TypeInfo).ToArray(),
                values
            );
        }

        /// <summary>
        /// Resolves a method within the type, assuming it's the only one with such name.
        /// </summary>
        public MethodWrapper ResolveMethod(TypeEntry type, string name, Func<IEnumerable<MethodWrapper>, MethodWrapper> filter = null)
        {
            var group = ResolveMethodGroup(type, name);
            return filter == null
                ? group.Single()
                : filter(group);
        }

        /// <summary>
        /// Finds an extension method for current type.
        /// </summary>
        public MethodWrapper ResolveExtensionMethod(TypeEntry type, string name, TypeEntry[] argTypes, TypeEntry[] hints = null, LambdaResolver lambdaResolver = null)
        {
            // the lookup is reflection over the referenced assemblies, so every type involved has to
            // have a CLR type behind it. An analysis-only run has built no assembly, so a signature
            // that mentions a declaration or a type parameter has nothing to materialize: there is
            // no extension method to be found, which is what a caller expecting one is told
            if (!IsEmitting && (HasNoRuntimeType(type) || HasNoRuntimeType(argTypes) || HasNoRuntimeType(hints)))
                throw new KeyNotFoundException();

            return ReflectionHelper.ResolveExtensionMethod(Resolver, _extensionResolver, type.Materialize(), name, TypeEntry.Materialize(argTypes), TypeEntry.Materialize(hints), lambdaResolver);
        }

        /// <summary>
        /// Whether a type cannot be turned into a CLR type before the assembly is emitted.
        /// </summary>
        private static bool HasNoRuntimeType(TypeEntry type)
        {
            return !ReferenceEquals(type, null) && type.ContainsDeclared;
        }

        /// <summary>
        /// Whether any type in a list cannot be turned into a CLR type before the assembly is emitted.
        /// </summary>
        private static bool HasNoRuntimeType(TypeEntry[] types)
        {
            return types != null && types.Any(HasNoRuntimeType);
        }

        /// <summary>
        /// Resolves a group of methods by name.
        /// Only non-generic methods are returned!
        /// </summary>
        public IEnumerable<MethodWrapper> ResolveMethodGroup(TypeEntry type, string name)
        {
            if (type.IsGenericParameter)
            {
                foreach (var constraint in ResolveConstraintsOf(type))
                {
                    try
                    {
                        return ResolveMethodGroup(constraint, name);
                    }
                    catch (KeyNotFoundException)
                    {
                    }
                }

                throw new KeyNotFoundException();
            }

            var declared = FindDeclaredType(type);
            if (declared == null)
                return ResolveHostMethodGroup(type, name);

            return declared.Entity.ResolveMethodGroup(name).Select(x => WrapMethod(declared, x));
        }

        /// <summary>
        /// Resolves a conversion operator to a certain type.
        /// </summary>
        public MethodWrapper ResolveConvertorToType(TypeEntry from, TypeEntry to)
        {
            return ResolveMethodGroup(from, "op_Explicit").FirstOrDefault(x => x.ReturnType == to)
                   ?? ResolveMethodGroup(from, "op_Implicit").FirstOrDefault(x => x.ReturnType == to);
        }

        /// <summary>
        /// Resolves a global property by its name.
        /// </summary>
        internal GlobalPropertyInfo ResolveGlobalProperty(string name)
        {
            if (!_definedProperties.TryGetValue(name, out var ent))
                throw new KeyNotFoundException();

            return ent;
        }

        #region Declared type references

        /// <summary>
        /// A reference to a type declared in the script, which may be a constructed instantiation
        /// of a generic one. This is the single place that knows how to reach a member of a
        /// constructed generic type whose definition is still a TypeBuilder: calling GetField or
        /// GetMethod on such a type throws, and the static TypeBuilder helpers must be used instead.
        /// </summary>
        private class DeclaredTypeReference
        {
            public TypeEntity Entity;

            /// <summary>
            /// The resolution context, which the entry-space substitution below needs.
            /// </summary>
            public TypeResolutionContext Resolver;

            /// <summary>
            /// The constructed generic type, or null when the reference is to the definition itself.
            /// </summary>
            public TypeEntry Instantiation;

            /// <summary>
            /// The type as it is referred to at the use site.
            /// </summary>
            public TypeEntry Type => Instantiation ?? Entity.TypeInfo;

            /// <summary>
            /// The base type of the reference, with the instantiation applied.
            /// </summary>
            public TypeEntry BaseType => Substitute(Entity.Parent ?? TypeEntryCache.Of<object>());

            /// <summary>
            /// Rewrites a type that may mention the definition's parameters in terms of the
            /// actual type arguments.
            /// </summary>
            public TypeEntry Substitute(TypeEntry type)
            {
                if (Instantiation == null)
                    return type;

                // the declaration's own parameters, not the ones its entry reports: an entity that
                // has not been emitted has no parameter builders, and this has to work anyway
                var parameters = Entity.GenericParameters.Select(x => x.TypeInfo).ToArray();

                return ConstructedTypeEntry.SubstituteInto(Resolver, type, parameters, Instantiation.GenericArguments);
            }

            /// <summary>
            /// Returns the version of a field that belongs to the constructed type.
            ///
            /// A builder that does not exist yet has no per-instantiation version either: analysis
            /// never needs one, and by the time emission does, the builders are all there.
            /// </summary>
            public FieldInfo MemberOf(FieldBuilder field)
            {
                return Instantiation == null || field == null ? (FieldInfo) field : TypeBuilder.GetField(Instantiation.Materialize(), field);
            }

            /// <summary>
            /// Returns the version of a constructor that belongs to the constructed type.
            /// </summary>
            public ConstructorInfo MemberOf(ConstructorBuilder ctor)
            {
                return Instantiation == null || ctor == null ? (ConstructorInfo) ctor : TypeBuilder.GetConstructor(Instantiation.Materialize(), ctor);
            }

            /// <summary>
            /// Returns the version of a method that belongs to the constructed type.
            /// </summary>
            public MethodInfo MemberOf(MethodInfo method)
            {
                return Instantiation == null || !(method is MethodBuilder)
                    ? method
                    : TypeBuilder.GetMethod(Instantiation.Materialize(), (MethodBuilder) method);
            }
        }

        /// <summary>
        /// Checks whether a type is declared in the script, possibly as an instantiation of a
        /// declared generic type.
        /// </summary>
        public bool IsDeclaredType(TypeEntry type)
        {
            return FindDeclaredType(type) != null;
        }

        /// <summary>
        /// Checks whether a type is declared in the script, and if so, whether it is a constructed
        /// instantiation of a declared generic type.
        /// </summary>
        private DeclaredTypeReference FindDeclaredType(TypeEntry type)
        {
            if (type == null)
                return null;

            // the entry knows what it is. This used to test 'type is TypeBuilder' and then look the
            // declaration back up by its emitted name, which meant a declaration could only be
            // recognised once it had a builder - the chicken and egg that made analysing a script
            // require emitting it.
            if (type is TypeEntityEntry declared)
                return new DeclaredTypeReference {Entity = declared.Entity, Resolver = Resolver};

            if (type.IsGenericType && !type.IsGenericTypeDefinition && type.GenericDefinition is TypeEntityEntry definition)
                return new DeclaredTypeReference {Entity = definition.Entity, Resolver = Resolver, Instantiation = type};

            return null;
        }

        /// <summary>
        /// Returns the types whose members are reachable through a generic parameter:
        /// its interface constraints, its base type constraint, and finally object.
        /// </summary>
        private IEnumerable<TypeEntry> ResolveConstraintsOf(TypeEntry typeParameter)
        {
            foreach (var iface in Resolver.ResolveInterfaces(typeParameter.Materialize()))
                yield return TypeEntryCache.Of(iface);

            var entity = Resolver.FindConstraints(typeParameter);
            if (entity?.BaseType != null && !entity.BaseType.IsGenericParameter)
                yield return entity.BaseType;

            yield return TypeEntryCache.Of<object>();
        }

        #endregion

        #region Members of a host type

        // The lookups below are the single path by which a member of a host type is found, whether or
        // not an assembly exists. Each of them resolves structurally: the member is looked up on the
        // generic definition when the type itself is an instantiation that reflection cannot answer
        // for, and its signature is rewritten into the instantiation's terms in entry space. The
        // reflection object - the FieldInfo, the MethodInfo, the accessors - is not produced here at
        // all: the wrapper is handed the recipe for it and follows the recipe when emission asks.
        //
        // This is what used to be two paths, one for analysis and one for emission, the latter going
        // through ReflectionHelper and materializing everything on the way. The emission half of each
        // of those was a NotSupportedException handler resolving on the definition and re-applying
        // the arguments by hand, which is precisely what the entry model does properly.

        /// <summary>
        /// Whether a type is an instantiation of a host generic definition over something the script
        /// declared: List&lt;SomeRecord&gt;, EqualityComparer&lt;T&gt;.
        ///
        /// The instantiation cannot be reflected on, but its definition is an ordinary host type, so
        /// a member is looked up there and its signature rewritten into the instantiation's terms.
        /// </summary>
        private static bool IsHostInstantiation(TypeEntry type)
        {
            return type != null
                   && type.ContainsDeclared
                   && type.IsGenericType
                   && !type.IsGenericTypeDefinition
                   && !type.GetGenericDefinition().IsDeclared;
        }

        /// <summary>
        /// The instantiation a member has to be rewritten into the terms of, or null when the member
        /// is looked up on the type itself.
        /// </summary>
        private static TypeEntry InstantiationOf(TypeEntry type)
        {
            return IsHostInstantiation(type) ? type : null;
        }

        /// <summary>
        /// The CLR type a member of the given type is looked up on: the type itself, or the
        /// definition behind it when the type is an instantiation with no CLR counterpart.
        ///
        /// A type that is still made of declarations once that step has been taken - a generic
        /// parameter, an array of one - cannot be reflected on at all, and a member of it is simply
        /// not found. That is what the reflection path reported too, through the branch of its
        /// NotSupportedException handler that gave up on a non-generic type.
        /// </summary>
        private static Type LookupTargetOf(TypeEntry type)
        {
            var target = IsHostInstantiation(type) ? type.GetGenericDefinition() : type;

            if (target == null || target.ContainsDeclared)
                throw new KeyNotFoundException();

            return target.Materialize();
        }

        /// <summary>
        /// Rewrites a member's declared signature into the terms of the instantiation it was reached
        /// through, doing nothing when the member was found on the type itself.
        /// </summary>
        private TypeEntry SubstituteIntoInstantiation(Type declared, TypeEntry instantiation)
        {
            var type = TypeEntryCache.Of(declared);
            if (instantiation == null)
                return type;

            return SubstituteIntoInstantiation(
                type,
                instantiation.GetGenericDefinition().GenericArguments,
                instantiation.GenericArguments
            );
        }

        /// <summary>
        /// Replaces every occurrence of the definition's parameters in a member's signature with the
        /// corresponding type argument.
        ///
        /// This walks the signature itself and defers to the model's substitution for the leaves,
        /// because the model leaves a generic type *definition* alone: a definition's arguments are
        /// its own parameters, and nothing above member lookup needed those rewritten. A member
        /// signature does - the type of EqualityComparer&lt;T&gt;.Default is spelled as the
        /// definition itself, and so is the argument of its GetHashCode - and reflection's
        /// ApplyGenericArguments always did rewrite it. The right place for this is
        /// ConstructedTypeEntry.SubstituteInto.
        /// </summary>
        private TypeEntry SubstituteIntoInstantiation(TypeEntry type, TypeEntry[] parameters, TypeEntry[] arguments)
        {
            if (type == null)
                return null;

            return ConstructedTypeEntry.SubstituteInto(Resolver, type, parameters, arguments);
        }

        /// <summary>
        /// The version of a field that belongs to the instantiation it was reached through.
        /// </summary>
        private static FieldInfo MemberOfInstantiation(FieldInfo field, TypeEntry instantiation)
        {
            return instantiation == null ? field : TypeBuilder.GetField(instantiation.Materialize(), field);
        }

        /// <summary>
        /// The version of a constructor that belongs to the instantiation it was reached through.
        /// </summary>
        private static ConstructorInfo MemberOfInstantiation(ConstructorInfo ctor, TypeEntry instantiation)
        {
            return instantiation == null ? ctor : TypeBuilder.GetConstructor(instantiation.Materialize(), ctor);
        }

        /// <summary>
        /// The version of a method that belongs to the instantiation it was reached through.
        ///
        /// The method was found on the definition, and an interface method may have been found on a
        /// different definition than the one the instantiation names, so the declaring type is
        /// recovered the way the reflection path recovered it.
        /// </summary>
        private MethodInfo MemberOfInstantiation(MethodInfo method, TypeEntry instantiation)
        {
            if (method == null || instantiation == null)
                return method;

            var type = instantiation.Materialize();
            var declaringType = ReflectionHelper.ResolveActualDeclaringType(Resolver, type, method.DeclaringType);

            return ReflectionHelper.GetMethodVersionForType(declaringType, method);
        }

        /// <summary>
        /// Resolves a field of a host type.
        /// </summary>
        private FieldWrapper ResolveHostField(TypeEntry type, string name)
        {
            var instantiation = InstantiationOf(type);
            var field = LookupTargetOf(type).GetField(name);
            if (field == null)
                throw new KeyNotFoundException();

            return new FieldWrapper
            {
                Name = name,
                DeclaringType = type,

                IsStatic = field.IsStatic,
                IsLiteral = field.IsLiteral,
                FieldType = SubstituteIntoInstantiation(field.FieldType, instantiation),

                FieldInfoSource = () => MemberOfInstantiation(field, instantiation)
            };
        }

        /// <summary>
        /// Resolves a property of a host type.
        /// </summary>
        private PropertyWrapper ResolveHostProperty(TypeEntry type, string name)
        {
            var instantiation = InstantiationOf(type);
            var pty = LookupTargetOf(type).GetProperty(name);
            if (pty == null)
                throw new KeyNotFoundException();

            var getter = pty.GetGetMethod();
            var setter = pty.GetSetMethod();
            var any = getter ?? setter;

            return new PropertyWrapper
            {
                Name = name,
                DeclaringType = type,

                CanGet = getter != null,
                CanSet = setter != null,
                IsStatic = any.IsStatic,
                IsVirtual = any.IsVirtual,
                PropertyType = SubstituteIntoInstantiation(pty.PropertyType, instantiation),

                GetterSource = () => MemberOfInstantiation(getter, instantiation),
                SetterSource = () => MemberOfInstantiation(setter, instantiation)
            };
        }

        /// <summary>
        /// Resolves an event of a host type.
        /// </summary>
        private EventWrapper ResolveHostEvent(TypeEntry type, string name)
        {
            var evt = LookupTargetOf(type).GetEvent(name);
            if (evt == null)
                throw new KeyNotFoundException();

            var instantiation = InstantiationOf(type);
            var adder = evt.GetAddMethod();
            var remover = evt.GetRemoveMethod();

            return new EventWrapper
            {
                Name = name,
                DeclaringType = type,

                IsStatic = remover.IsStatic,
                EventHandlerType = SubstituteIntoInstantiation(evt.EventHandlerType, instantiation),

                AddMethodSource = () => MemberOfInstantiation(adder, instantiation),
                RemoveMethodSource = () => MemberOfInstantiation(remover, instantiation)
            };
        }

        /// <summary>
        /// Resolves a constructor of a host type.
        /// </summary>
        private ConstructorWrapper ResolveHostConstructor(TypeEntry type, TypeEntry[] argTypes)
        {
            var instantiation = InstantiationOf(type);
            var found = ReflectionHelper.ResolveMethodByArgs(
                Resolver,
                LookupTargetOf(type).GetConstructors(),
                c => c.GetParameters().Select(p => SubstituteIntoInstantiation(p.ParameterType, instantiation)).ToArray(),
                ReflectionHelper.IsVariadic,
                argTypes
            );

            var ctor = found.Method;

            return new ConstructorWrapper
            {
                DeclaringType = type,
                ArgumentTypes = found.ArgumentTypes,

                IsPartiallyApplied = ReflectionHelper.IsPartiallyApplied(argTypes),
                IsVariadic = ReflectionHelper.IsVariadic(ctor),

                ConstructorInfoSource = () => MemberOfInstantiation(ctor, instantiation)
            };
        }

        /// <summary>
        /// Resolves a method of a host type by name and argument types.
        /// </summary>
        private MethodWrapper ResolveHostMethod(TypeEntry type, string name, TypeEntry[] argTypes, TypeEntry[] hints, LambdaResolver lambdaResolver)
        {
            var instantiation = InstantiationOf(type);
            var found = ReflectionHelper.ResolveMethodByArgs(
                Resolver,
                ReflectionHelper.GetMethodsByName(LookupTargetOf(type), name),
                m => m.GetParameters().Select(p => SubstituteIntoInstantiation(p.ParameterType, instantiation)).ToArray(),
                ReflectionHelper.IsVariadic,
                argTypes
            );

            var info = found.Method;
            var mw = new MethodWrapper
            {
                Name = name,
                DeclaringType = type,

                IsStatic = info.IsStatic,
                IsVirtual = info.IsVirtual,
                IsPartiallyApplied = ReflectionHelper.IsPartiallyApplied(argTypes),
                IsVariadic = ReflectionHelper.IsVariadic(info),

                ArgumentTypes = found.ArgumentTypes,
                ReturnType = SubstituteIntoInstantiation(info.ReturnType, instantiation),

                MethodInfoSource = () => MemberOfInstantiation(info, instantiation)
            };

            if (info.IsGenericMethod)
            {
                var parameters = TypeEntryCache.Of(info.GetGenericArguments());
                var values = InferMethodGenerics(found, parameters, argTypes, hints, lambdaResolver);

                mw.GenericArguments = values;
                mw.ArgumentTypes = found.ArgumentTypes.Select(x => SubstituteIntoInstantiation(x, parameters, values)).ToArray();
                mw.ReturnType = SubstituteIntoInstantiation(mw.ReturnType, parameters, values);
                mw.MethodInfoSource = () => MemberOfInstantiation(info, instantiation).MakeGenericMethod(TypeEntry.Materialize(values));
            }
            else if (hints != null)
            {
                Error(CompilerMessages.GenericArgsToNonGenericMethod, name);
            }

            CheckFixedParameters(mw, argTypes);

            return mw;
        }

        /// <summary>
        /// Refuses a call that only matched because overload resolution treats a generic parameter as
        /// something anything can be stored into.
        ///
        /// That is true of a generic method's own parameters, which is exactly what inference is for,
        /// and it is why the check cannot live inside the distance calculation. It is not true of the
        /// parameters an instantiation carries: inside a generic function, an arr of List&lt;T&gt;
        /// takes a T in Add and nothing else, so 'arr.Add "test"' is a mistake rather than something a
        /// later pass will settle. By this point every parameter inference could fill in has been
        /// substituted, so whatever is still a bare parameter is fixed.
        /// </summary>
        private static void CheckFixedParameters(MethodWrapper method, TypeEntry[] argTypes)
        {
            var expected = method.ArgumentTypes;
            if (expected == null || argTypes == null || method.IsPartiallyApplied || method.IsVariadic)
                return;

            var count = Math.Min(expected.Length, argTypes.Length);

            for (var idx = 0; idx < count; idx++)
            {
                var want = expected[idx];
                var got = argTypes[idx];

                if (ReferenceEquals(want, null) || ReferenceEquals(got, null) || !want.IsGenericParameter)
                    continue;

                // null reaches a parameter of any reference type, and whether a T is one is decided
                // by its constraints rather than here
                if (want == got || got.Is<NullType>() || got.Is<UnspecifiedType>())
                    continue;

                Error(CompilerMessages.ArgumentTypeMismatch, got, want);
            }
        }

        /// <summary>
        /// Works out the type arguments of a generic host method from the call site.
        ///
        /// Two engines, and the choice between them is a question of what can be expressed rather
        /// than of which half of the compilation is running. The CLR-side one infers through lambdas,
        /// which is what a call like 'ConvertAll (x -> x * 2)' needs, but it can only match types the
        /// CLR has; the entry-side one matches structurally and is the only one that can say anything
        /// about a signature made of declarations.
        /// </summary>
        private TypeEntry[] InferMethodGenerics(MethodLookupResult<MethodInfo> found, TypeEntry[] parameters, TypeEntry[] argTypes, TypeEntry[] hints, LambdaResolver lambdaResolver)
        {
            var declared = found.ArgumentTypes.Any(x => x != null && x.ContainsDeclared)
                           || argTypes.Any(x => x != null && x.ContainsDeclared)
                           || (hints != null && hints.Any(x => x != null && x.ContainsDeclared));

            if (declared)
                return ReflectionHelper.InferGenericArguments(Resolver, parameters, found.ArgumentTypes, argTypes, hints);

            var values = GenericHelper.ResolveMethodGenericsByArgs(
                Resolver,
                TypeEntry.Materialize(found.ArgumentTypes),
                TypeEntry.Materialize(argTypes),
                TypeEntry.Materialize(parameters),
                TypeEntry.Materialize(hints),
                lambdaResolver
            );

            return TypeEntryCache.Of(values);
        }

        /// <summary>
        /// Resolves the non-generic methods of a given name on a host type.
        /// </summary>
        private IEnumerable<MethodWrapper> ResolveHostMethodGroup(TypeEntry type, string name)
        {
            var instantiation = InstantiationOf(type);

            return ReflectionHelper.GetMethodsByName(LookupTargetOf(type), name)
                                   .Where(m => !m.IsGenericMethod)
                                   .Select(
                                       m => new MethodWrapper
                                       {
                                           Name = name,
                                           DeclaringType = type,

                                           IsStatic = m.IsStatic,
                                           IsVirtual = m.IsVirtual,
                                           IsVariadic = ReflectionHelper.IsVariadic(m),

                                           ArgumentTypes = m.GetParameters().Select(p => SubstituteIntoInstantiation(p.ParameterType, instantiation)).ToArray(),
                                           ReturnType = SubstituteIntoInstantiation(m.ReturnType, instantiation),

                                           MethodInfoSource = () => MemberOfInstantiation(m, instantiation)
                                       }
                                   )
                                   .ToArray();
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Resolves a lambda return type when its argument types have been inferred from usage.
        /// </summary>
        public TypeEntry ResolveLambda(LambdaNode lambda, TypeEntry[] argTypes)
        {
            lambda.SetInferredArgumentTypes(this, argTypes);
            var delegateType = lambda.Resolve(this);
            return ReflectionHelper.WrapDelegate(Resolver, delegateType.Materialize()).ReturnType;
        }

        /// <summary>
        /// Creates a wrapper from a method entity.
        /// </summary>
        private MethodWrapper WrapMethod(DeclaredTypeReference declared, MethodEntity method, bool isPartial = false)
        {
            return new MethodWrapper
            {
                Name = method.Name,
                DeclaringType = declared.Type,

                IsStatic = method.IsStatic,
                IsVirtual = method.IsVirtual,
                IsPartiallyApplied = isPartial,
                IsVariadic = method.IsVariadic,

                MethodInfoSource = () => declared.MemberOf(method.MethodInfo),
                ArgumentTypes = method.GetArgumentTypes(this).Select(declared.Substitute).ToArray(),
                ReturnType = declared.Substitute(method.ReturnType)
            };
        }

        #endregion
    }
}