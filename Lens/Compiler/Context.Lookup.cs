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
        /// Resolves a type by its signature.
        /// </summary>
        public TypeEntry ResolveType(TypeSignature signature, bool allowUnspecified = false)
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
                    return TypeEntryCache.Of(GenericHelper.MakeGenericTypeChecked(Resolver, genericDeclared.TypeInfo.Materialize(), TypeEntry.Materialize(args)));
                }
            }

            return TypeEntryCache.Of(_typeResolver.ResolveType(signature));
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

            return TypeEntryCache.Of(Resolver.MakeGenericType(entity.TypeBuilder, TypeEntry.Materialize(arguments)));
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

                try
                {
                    curr = curr.BaseType;
                }
                catch (NotSupportedException)
                {
                    return null;
                }
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
                return ReflectionHelper.ResolveField(Resolver, type.Materialize(), name);

            var fi = declared.Entity.ResolveField(name);
            return new FieldWrapper
            {
                Name = name,
                DeclaringType = type,

                FieldInfo = declared.MemberOf(fi.FieldBuilder),
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
                return ReflectionHelper.ResolveProperty(Resolver, type.Materialize(), name);

            // no internal properties
            throw new KeyNotFoundException();
        }

        /// <summary>
        /// Resolves an event from a type by its name.
        /// </summary>
        public EventWrapper ResolveEvent(TypeEntry type, string name)
        {
            if (FindDeclaredType(type) == null)
                return ReflectionHelper.ResolveEvent(Resolver, type.Materialize(), name);

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
                return ReflectionHelper.ResolveConstructor(Resolver, type.Materialize(), TypeEntry.Materialize(argTypes));

            var ctor = declared.Entity.ResolveConstructor(argTypes, declared.Instantiation);

            return new ConstructorWrapper
            {
                DeclaringType = type,
                ConstructorInfo = declared.MemberOf(ctor.ConstructorBuilder),
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
                return ReflectionHelper.ResolveMethod(Resolver, type.Materialize(), name, argTypes, hints, resolver);

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
            return ReflectionHelper.ResolveExtensionMethod(Resolver, _extensionResolver, type.Materialize(), name, TypeEntry.Materialize(argTypes), TypeEntry.Materialize(hints), lambdaResolver);
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
                return ReflectionHelper.ResolveMethodGroup(Resolver, type.Materialize(), name);

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
                return Instantiation == null ? type : TypeEntryCache.Of(GenericHelper.ApplyGenericArguments(type.Materialize(), Instantiation.Materialize(), false));
            }

            /// <summary>
            /// Returns the version of a field that belongs to the constructed type.
            /// </summary>
            public FieldInfo MemberOf(FieldBuilder field)
            {
                return Instantiation == null ? (FieldInfo) field : TypeBuilder.GetField(Instantiation.Materialize(), field);
            }

            /// <summary>
            /// Returns the version of a constructor that belongs to the constructed type.
            /// </summary>
            public ConstructorInfo MemberOf(ConstructorBuilder ctor)
            {
                return Instantiation == null ? (ConstructorInfo) ctor : TypeBuilder.GetConstructor(Instantiation.Materialize(), ctor);
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
                return new DeclaredTypeReference {Entity = declared.Entity};

            if (type.IsGenericType && !type.IsGenericTypeDefinition && type.GenericDefinition is TypeEntityEntry definition)
                return new DeclaredTypeReference {Entity = definition.Entity, Instantiation = type};

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

                MethodInfo = declared.MemberOf(method.MethodInfo),
                ArgumentTypes = method.GetArgumentTypes(this).Select(declared.Substitute).ToArray(),
                ReturnType = declared.Substitute(method.ReturnType)
            };
        }

        #endregion
    }
}