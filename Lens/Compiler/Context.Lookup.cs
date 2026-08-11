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
        public Type ResolveType(string signature)
        {
            return ResolveType(TypeSignature.Parse(signature));
        }

        /// <summary>
        /// Resolves a type by its signature.
        /// </summary>
        public Type ResolveType(TypeSignature signature, bool allowUnspecified = false)
        {
            if (allowUnspecified && signature.FullSignature == "_")
                return null;

            // a generic parameter of the enclosing declaration wins over anything else
            if (signature.Arguments == null && string.IsNullOrEmpty(signature.Postfix))
            {
                var typeParam = Resolver.FindTypeParameter(signature.Name);
                if (typeParam?.Builder != null)
                    return typeParam.Builder;
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
        public Type ResolvePatternType(TypeEntity entity, Type expressionType)
        {
            if (!entity.IsGeneric)
                return entity.TypeInfo;

            var arguments = FindTypeArguments(expressionType, entity.GenericParameterCount);
            if (arguments == null)
                Error(CompilerMessages.GenericTypeArgCountMismatch, entity.Name, entity.GenericParameterCount, 0);

            return Resolver.MakeGenericType(entity.TypeBuilder, arguments);
        }

        /// <summary>
        /// Looks for the type arguments of the matched value, walking up its inheritance chain.
        /// </summary>
        private static Type[] FindTypeArguments(Type type, int count)
        {
            var curr = type;
            while (curr != null)
            {
                if (curr.IsGenericType)
                {
                    var args = curr.GetGenericArguments();
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
        public FieldWrapper ResolveField(Type type, string name)
        {
            var declared = FindDeclaredType(type);
            if (declared == null)
                return ReflectionHelper.ResolveField(Resolver, type, name);

            var fi = declared.Entity.ResolveField(name);
            return new FieldWrapper
            {
                Name = name,
                DeclaringType = TypeEntryCache.Of(type),

                FieldInfo = declared.MemberOf(fi.FieldBuilder),
                IsStatic = fi.IsStatic,
                FieldType = TypeEntryCache.Of(declared.Substitute(fi.FieldBuilder.FieldType))
            };
        }

        /// <summary>
        /// Resolves a property from a type by its name.
        /// </summary>
        public PropertyWrapper ResolveProperty(Type type, string name)
        {
            if (FindDeclaredType(type) == null)
                return ReflectionHelper.ResolveProperty(Resolver, type, name);

            // no internal properties
            throw new KeyNotFoundException();
        }

        /// <summary>
        /// Resolves an event from a type by its name.
        /// </summary>
        public EventWrapper ResolveEvent(Type type, string name)
        {
            if (FindDeclaredType(type) == null)
                return ReflectionHelper.ResolveEvent(Resolver, type, name);

            // no internal events
            throw new KeyNotFoundException();
        }

        /// <summary>
        /// Resolves a constructor from a type by the list of arguments.
        /// </summary>
        public ConstructorWrapper ResolveConstructor(Type type, Type[] argTypes)
        {
            var declared = FindDeclaredType(type);
            if (declared == null)
                return ReflectionHelper.ResolveConstructor(Resolver, type, argTypes);

            var ctor = declared.Entity.ResolveConstructor(argTypes, declared.Instantiation);

            return new ConstructorWrapper
            {
                DeclaringType = TypeEntryCache.Of(type),
                ConstructorInfo = declared.MemberOf(ctor.ConstructorBuilder),
                ArgumentTypes = ctor.GetArgumentTypes(this).Select(declared.Substitute).Select(TypeEntryCache.Of).ToArray(),

                IsPartiallyApplied = ReflectionHelper.IsPartiallyApplied(argTypes),
                IsVariadic = false // built-in ctors can't do that
            };
        }

        /// <summary>
        /// Resolves a method by its name and argument types. If generic arguments are passed, they are also applied.
        /// Generic arguments whose values can be inferred from argument types can be skipped.
        /// </summary>
        public MethodWrapper ResolveMethod(Type type, string name, Type[] argTypes, Type[] hints = null, LambdaResolver resolver = null)
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
                return ReflectionHelper.ResolveMethod(Resolver, type, name, argTypes, hints, resolver);

            try
            {
                var method = declared.Entity.ResolveMethod(name, argTypes, instantiation: declared.Instantiation);
                var mw = WrapMethod(declared, method, ReflectionHelper.IsPartiallyApplied(argTypes));

                if (method.IsImported && method.MethodInfo.IsGenericMethod)
                {
                    var argTypeDefs = method.MethodInfo.GetParameters().Select(p => p.ParameterType).ToArray();
                    var genericDefs = method.MethodInfo.GetGenericArguments();
                    var genericValues = GenericHelper.ResolveMethodGenericsByArgs(Resolver, argTypeDefs, argTypes, genericDefs, hints, resolver);

                    mw.MethodInfo = method.MethodInfo.MakeGenericMethod(genericValues);
                    mw.ArgumentTypes = method.GetArgumentTypes(this).Select(t => TypeEntryCache.Of(GenericHelper.ApplyGenericArguments(t, genericDefs, genericValues))).ToArray();
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
        private void InstantiateGenericMethod(MethodWrapper mw, MethodEntity method, Type[] argTypes, Type[] hints, LambdaResolver resolver)
        {
            var genericDefs = method.GenericParameters.Select(p => (Type) p.Builder).ToArray();

            if (hints != null && hints.Length != genericDefs.Length)
                Error(CompilerMessages.GenericArgCountMismatch);

            var argTypeDefs = method.GetArgumentTypes(this);
            var genericValues = GenericHelper.ResolveMethodGenericsByArgs(Resolver, argTypeDefs, argTypes, genericDefs, hints, resolver);

            GenericHelper.CheckConstraints(Resolver, method.GenericParameters, genericValues);

            mw.MethodInfo = method.MethodBuilder.MakeGenericMethod(genericValues);
            mw.ArgumentTypes = argTypeDefs.Select(t => TypeEntryCache.Of(GenericHelper.ApplyGenericArguments(t, genericDefs, genericValues))).ToArray();
            mw.GenericArguments = genericValues.Select(TypeEntryCache.Of).ToArray();
            mw.ReturnType = TypeEntryCache.Of(GenericHelper.ApplyGenericArguments(method.ReturnType, genericDefs, genericValues));
        }

        /// <summary>
        /// Resolves a method within the type, assuming it's the only one with such name.
        /// </summary>
        public MethodWrapper ResolveMethod(Type type, string name, Func<IEnumerable<MethodWrapper>, MethodWrapper> filter = null)
        {
            var group = ResolveMethodGroup(type, name);
            return filter == null
                ? group.Single()
                : filter(group);
        }

        /// <summary>
        /// Finds an extension method for current type.
        /// </summary>
        public MethodWrapper ResolveExtensionMethod(Type type, string name, Type[] argTypes, Type[] hints = null, LambdaResolver lambdaResolver = null)
        {
            return ReflectionHelper.ResolveExtensionMethod(Resolver, _extensionResolver, type, name, argTypes, hints, lambdaResolver);
        }

        /// <summary>
        /// Resolves a group of methods by name.
        /// Only non-generic methods are returned!
        /// </summary>
        public IEnumerable<MethodWrapper> ResolveMethodGroup(Type type, string name)
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
                return ReflectionHelper.ResolveMethodGroup(Resolver, type, name);

            return declared.Entity.ResolveMethodGroup(name).Select(x => WrapMethod(declared, x));
        }

        /// <summary>
        /// Resolves a conversion operator to a certain type.
        /// </summary>
        public MethodWrapper ResolveConvertorToType(Type from, Type to)
        {
            var toEntry = TypeEntryCache.Of(to);
            return ResolveMethodGroup(from, "op_Explicit").FirstOrDefault(x => x.ReturnType == toEntry)
                   ?? ResolveMethodGroup(from, "op_Implicit").FirstOrDefault(x => x.ReturnType == toEntry);
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
            public Type Instantiation;

            /// <summary>
            /// The type as it is referred to at the use site.
            /// </summary>
            public Type Type => Instantiation ?? Entity.TypeInfo;

            /// <summary>
            /// The base type of the reference, with the instantiation applied.
            /// </summary>
            public Type BaseType => Substitute(Entity.Parent ?? typeof(object));

            /// <summary>
            /// Rewrites a type that may mention the definition's parameters in terms of the
            /// actual type arguments.
            /// </summary>
            public Type Substitute(Type type)
            {
                return Instantiation == null ? type : GenericHelper.ApplyGenericArguments(type, Instantiation, false);
            }

            /// <summary>
            /// Returns the version of a field that belongs to the constructed type.
            /// </summary>
            public FieldInfo MemberOf(FieldBuilder field)
            {
                return Instantiation == null ? (FieldInfo) field : TypeBuilder.GetField(Instantiation, field);
            }

            /// <summary>
            /// Returns the version of a constructor that belongs to the constructed type.
            /// </summary>
            public ConstructorInfo MemberOf(ConstructorBuilder ctor)
            {
                return Instantiation == null ? (ConstructorInfo) ctor : TypeBuilder.GetConstructor(Instantiation, ctor);
            }

            /// <summary>
            /// Returns the version of a method that belongs to the constructed type.
            /// </summary>
            public MethodInfo MemberOf(MethodInfo method)
            {
                return Instantiation == null || !(method is MethodBuilder)
                    ? method
                    : TypeBuilder.GetMethod(Instantiation, (MethodBuilder) method);
            }
        }

        /// <summary>
        /// Checks whether a type is declared in the script, possibly as an instantiation of a
        /// declared generic type.
        /// </summary>
        public bool IsDeclaredType(Type type)
        {
            return FindDeclaredType(type) != null;
        }

        /// <summary>
        /// Checks whether a type is declared in the script, and if so, whether it is a constructed
        /// instantiation of a declared generic type.
        /// </summary>
        private DeclaredTypeReference FindDeclaredType(Type type)
        {
            if (type == null)
                return null;

            if (type is TypeBuilder)
            {
                var entity = FindTypeByEmittedName(type.Name);
                return entity == null ? null : new DeclaredTypeReference {Entity = entity};
            }

            if (type.IsGenericType && !type.IsGenericTypeDefinition && type.GetGenericTypeDefinition() is TypeBuilder definition)
            {
                var entity = FindTypeByEmittedName(definition.Name);
                if (entity != null)
                    return new DeclaredTypeReference {Entity = entity, Instantiation = type};
            }

            return null;
        }

        /// <summary>
        /// Returns the types whose members are reachable through a generic parameter:
        /// its interface constraints, its base type constraint, and finally object.
        /// </summary>
        private IEnumerable<Type> ResolveConstraintsOf(Type typeParameter)
        {
            foreach (var iface in Resolver.ResolveInterfaces(typeParameter))
                yield return iface;

            var entity = Resolver.FindConstraints(typeParameter);
            if (entity?.BaseType != null && !entity.BaseType.IsGenericParameter)
                yield return entity.BaseType;

            yield return typeof(object);
        }

        /// <summary>
        /// Finds a declared type by the name it is emitted under, which for a generic type
        /// carries the arity suffix that LENS itself never uses.
        /// </summary>
        private TypeEntity FindTypeByEmittedName(string name)
        {
            var tick = name.IndexOf('`');
            return FindType(tick < 0 ? name : name.Substring(0, tick));
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Resolves a lambda return type when its argument types have been inferred from usage.
        /// </summary>
        public Type ResolveLambda(LambdaNode lambda, Type[] argTypes)
        {
            lambda.SetInferredArgumentTypes(this, argTypes);
            var delegateType = lambda.Resolve(this);
            return ReflectionHelper.WrapDelegate(Resolver, delegateType).ReturnType.Materialize();
        }

        /// <summary>
        /// Creates a wrapper from a method entity.
        /// </summary>
        private MethodWrapper WrapMethod(DeclaredTypeReference declared, MethodEntity method, bool isPartial = false)
        {
            return new MethodWrapper
            {
                Name = method.Name,
                DeclaringType = TypeEntryCache.Of(declared.Type),

                IsStatic = method.IsStatic,
                IsVirtual = method.IsVirtual,
                IsPartiallyApplied = isPartial,
                IsVariadic = method.IsVariadic,

                MethodInfo = declared.MemberOf(method.MethodInfo),
                ArgumentTypes = method.GetArgumentTypes(this).Select(declared.Substitute).Select(TypeEntryCache.Of).ToArray(),
                ReturnType = TypeEntryCache.Of(declared.Substitute(method.ReturnType))
            };
        }

        #endregion
    }
}