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
        public MethodWrapper ResolveMethod(TypeEntry type, string name, TypeEntry[] argTypes, TypeEntry[] hints = null, LambdaResolver resolver = null, EntryLambdaResolver entryResolver = null)
        {
            // only the members of a parameter's constraints are available on it
            if (type.IsGenericParameter)
            {
                foreach (var constraint in ResolveConstraintsOf(type))
                {
                    try
                    {
                        return ResolveMethod(constraint, name, argTypes, hints, resolver, entryResolver);
                    }
                    catch (KeyNotFoundException)
                    {
                    }
                }

                throw new KeyNotFoundException();
            }

            var declared = FindDeclaredType(type);
            if (declared == null)
                return ResolveHostMethod(type, name, argTypes, hints, resolver, entryResolver);

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
                return ResolveMethod(declared.BaseType, name, argTypes, hints, resolver, entryResolver);
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
        private void InferGenericArgument(TypeEntry declared, TypeEntry actual, IList<GenericParameterEntity> parameters, TypeEntry[] values)
        {
            if (ReferenceEquals(declared, null) || ReferenceEquals(actual, null))
                return;

            var entity = (declared as GenericParameterEntry)?.Entity;
            if (entity != null)
            {
                var idx = parameters.IndexOf(entity);
                if (idx >= 0 && values[idx] == null)
                    values[idx] = actual;

                return;
            }

            if (declared.IsArray && actual.IsArray)
            {
                InferGenericArgument(declared.ElementType, actual.ElementType, parameters, values);
                return;
            }

            InferGenericArgumentsOfInstantiation(declared, actual, parameters, values);
        }

        /// <summary>
        /// Reads the values of the type parameters that a composite signature such as List&lt;T&gt;
        /// or Dictionary&lt;string, T&gt; mentions, off the type the call site actually passed.
        ///
        /// The signature names a definition applied to arguments, so the values sit one level down:
        /// whatever was passed names the same definition somewhere, and pairing the two argument
        /// lists reads the parameters off it. 'Somewhere' is why the base types and the interfaces
        /// are walked - a signature of IEnumerable&lt;T&gt; is routinely given a List.
        /// </summary>
        private void InferGenericArgumentsOfInstantiation(TypeEntry declared, TypeEntry actual, IList<GenericParameterEntity> parameters, TypeEntry[] values)
        {
            if (!declared.IsGenericType || declared.IsGenericTypeDefinition)
                return;

            var definition = declared.GetGenericDefinition();
            if (ReferenceEquals(definition, null))
                return;

            var instantiation = FindInstantiation(actual, definition);
            if (ReferenceEquals(instantiation, null))
                return;

            var declaredArgs = declared.GenericArguments;
            var actualArgs = instantiation.GenericArguments;
            var count = Math.Min(declaredArgs.Length, actualArgs.Length);

            for (var idx = 0; idx < count; idx++)
                InferGenericArgument(declaredArgs[idx], actualArgs[idx], parameters, values);
        }

        /// <summary>
        /// The instantiation of the given definition that a type is, inherits or implements.
        /// </summary>
        private TypeEntry FindInstantiation(TypeEntry type, TypeEntry definition)
        {
            for (var curr = type; !ReferenceEquals(curr, null); curr = curr.BaseType)
                if (IsInstantiationOf(curr, definition))
                    return curr;

            if (!definition.IsInterface)
                return null;

            foreach (var curr in type.GetInterfaces(Resolver))
                if (IsInstantiationOf(curr, definition))
                    return curr;

            return null;
        }

        private static bool IsInstantiationOf(TypeEntry type, TypeEntry definition)
        {
            return type.IsGenericType
                   && !type.IsGenericTypeDefinition
                   && type.GetGenericDefinition() == definition;
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
        /// Checks whether a type has a method of the given name, whatever its signature.
        ///
        /// This is what tells the two ways a call can fail apart: the type has nothing of that
        /// name, or it has, and none of the overloads takes what the call passes. Resolution
        /// itself cannot say which, because it reports both as a name that did not resolve.
        /// </summary>
        public bool HasMethodNamed(TypeEntry type, string name)
        {
            if (ReferenceEquals(type, null))
                return false;

            if (type.IsGenericParameter)
                return ResolveConstraintsOf(type).Any(x => HasMethodNamed(x, name));

            var declared = FindDeclaredType(type);
            if (declared == null)
            {
                try
                {
                    return ReflectionHelper.GetMethodsByName(LookupTargetOf(type), name).Any();
                }
                catch (KeyNotFoundException)
                {
                    return false;
                }
                catch (NotSupportedException)
                {
                    // a type that is still being built cannot be reflected over; the caller falls
                    // back to the message that claims nothing about the overloads
                    return false;
                }
            }

            try
            {
                if (declared.Entity.ResolveMethodGroup(name).Any())
                    return true;
            }
            catch (KeyNotFoundException)
            {
            }

            return HasMethodNamed(declared.BaseType, name);
        }

        /// <summary>
        /// Finds an extension method for current type.
        /// </summary>
        public MethodWrapper ResolveExtensionMethod(TypeEntry type, string name, TypeEntry[] argTypes, TypeEntry[] hints = null, LambdaResolver lambdaResolver = null, EntryLambdaResolver entryLambdaResolver = null)
        {
            // the candidates are host methods of referenced assemblies, but the receiver need not be
            // a host type: an extension method on a record the script declared, or on a list of one,
            // is an ordinary call, and weighing it is what the entry model is for. The reflection
            // path that used to be here could only be taken once an assembly existed, so analysis
            // reported every such call as a method that does not exist.
            var found = _extensionResolver.ResolveExtensionMethod(Resolver, type, name, argTypes);
            var declaringType = TypeEntryCache.Of(found.DeclaringType);

            // the receiver is checked wherever it came from, but the type the method is declared on
            // is never named in the script at all - the whole point of an extension method - so
            // this is the only place a rule about that type can be applied
            if (!IsTypeAllowed(declaringType))
                Error(CompilerMessages.SafeModeIllegalType, declaringType.FullName);

            var parameters = found.GetParameters().Select(p => TypeEntryCache.Of(p.ParameterType)).ToArray();
            var mw = new MethodWrapper
            {
                Name = name,
                DeclaringType = declaringType,

                IsStatic = true,
                IsVirtual = false,
                IsPartiallyApplied = ReflectionHelper.IsPartiallyApplied(argTypes),
                IsVariadic = ReflectionHelper.IsVariadic(found),

                ArgumentTypes = parameters,
                ReturnType = TypeEntryCache.Of(found.ReturnType),

                MethodInfoSource = () => found
            };

            if (found.IsGenericMethod)
            {
                // the receiver is argument zero of the method being called, and inference has to see
                // it as one: the TSource of an Enumerable overload is named nowhere else
                var genericDefs = TypeEntryCache.Of(found.GetGenericArguments());
                var actual = new[] {type}.Concat(argTypes).ToArray();
                var values = InferGenerics(genericDefs, parameters, actual, hints, lambdaResolver, entryLambdaResolver);

                mw.GenericArguments = values;
                mw.ArgumentTypes = parameters.Select(x => SubstituteIntoInstantiation(x, genericDefs, values)).ToArray();
                mw.ReturnType = SubstituteIntoInstantiation(mw.ReturnType, genericDefs, values);
                mw.MethodInfoSource = () => found.MakeGenericMethod(TypeEntry.Materialize(values));
            }
            else if (hints != null)
            {
                Error(CompilerMessages.GenericArgsToNonGenericMethod, name);
            }

            return mw;
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
        /// Resolves the indexer of a host type, given the type of the index expression.
        ///
        /// This is the same structural lookup as the members above, and exists for the same reason:
        /// an indexer used to be resolved by reflecting on the materialized type, so indexing a
        /// List&lt;T&gt; inside a generic function - a type whose argument is a parameter with no
        /// builder yet - asked a type parameter to materialize before there was an assembly.
        /// </summary>
        public MethodWrapper ResolveIndexer(TypeEntry type, TypeEntry idxType, bool isGetter)
        {
            var instantiation = InstantiationOf(type);
            var target = LookupTargetOf(type);

            var candidates = new List<Tuple<MethodInfo, TypeEntry, int>>();

            foreach (var pty in target.GetProperties())
            {
                var accessor = isGetter ? pty.GetGetMethod() : pty.GetSetMethod();
                if (accessor == null)
                    continue;

                var idxArgs = pty.GetIndexParameters();
                if (idxArgs.Length != 1)
                    continue;

                var argType = SubstituteIntoInstantiation(idxArgs[0].ParameterType, instantiation);

                candidates.Add(new Tuple<MethodInfo, TypeEntry, int>(accessor, argType, argType.DistanceFrom(Resolver, idxType)));
            }

            candidates.Sort((x, y) => x.Item3.CompareTo(y.Item3));

            if (candidates.Count == 0 || candidates[0].Item3 == int.MaxValue)
                Error(
                    isGetter ? CompilerMessages.IndexGetterNotFound : CompilerMessages.IndexSetterNotFound,
                    type,
                    idxType
                );

            if (candidates.Count > 1 && candidates[0].Item3 == candidates[1].Item3)
                Error(
                    CompilerMessages.IndexAmbigious,
                    type,
                    candidates[0].Item2,
                    candidates[1].Item2,
                    Environment.NewLine
                );

            var found = candidates[0].Item1;

            return new MethodWrapper
            {
                Name = found.Name,
                DeclaringType = type,

                IsStatic = false,
                IsVirtual = found.IsVirtual,

                ArgumentTypes = found.GetParameters().Select(p => SubstituteIntoInstantiation(p.ParameterType, instantiation)).ToArray(),
                ReturnType = SubstituteIntoInstantiation(found.ReturnType, instantiation),

                MethodInfoSource = () => MemberOfInstantiation(found, instantiation)
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
        private MethodWrapper ResolveHostMethod(TypeEntry type, string name, TypeEntry[] argTypes, TypeEntry[] hints, LambdaResolver lambdaResolver, EntryLambdaResolver entryLambdaResolver = null)
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
                var values = InferGenerics(parameters, found.ArgumentTypes, argTypes, hints, lambdaResolver, entryLambdaResolver);

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
        /// Works out the type arguments of a generic host call from the call site - an extension
        /// method call included, where the receiver is argument zero.
        ///
        /// Two engines, and the choice between them is a question of what can be expressed rather
        /// than of which half of the compilation is running. The CLR-side one can only match types
        /// the CLR has; the entry-side one matches structurally and is the only one that can say
        /// anything about a signature made of declarations. Both infer through a lambda, each with
        /// the resolver of its own kind.
        /// </summary>
        private TypeEntry[] InferGenerics(TypeEntry[] parameters, TypeEntry[] expectedTypes, TypeEntry[] argTypes, TypeEntry[] hints, LambdaResolver lambdaResolver, EntryLambdaResolver entryLambdaResolver)
        {
            var declared = expectedTypes.Any(x => x != null && x.ContainsDeclared)
                           || argTypes.Any(x => x != null && x.ContainsDeclared)
                           || (hints != null && hints.Any(x => x != null && x.ContainsDeclared));

            if (declared)
                return ReflectionHelper.InferGenericArguments(Resolver, parameters, expectedTypes, argTypes, hints, entryLambdaResolver);

            var values = GenericHelper.ResolveMethodGenericsByArgs(
                Resolver,
                TypeEntry.Materialize(expectedTypes),
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
            return WrapDelegate(lambda.Resolve(this)).ReturnType;
        }

        /// <summary>
        /// The signature of a delegate type, without asking the CLR for one.
        ///
        /// Func&lt;SomeRecord, int&gt; is an ordinary delegate whose Invoke says exactly what the
        /// call site needs to know, and the entry-side member lookup can read it off the definition
        /// and rewrite it into the instantiation's terms. Going through reflection instead would
        /// materialize the record, which is what analysis exists not to do.
        /// </summary>
        public MethodWrapper WrapDelegate(TypeEntry type)
        {
            return ReflectionHelper.WrapDelegate(Resolver, type);
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