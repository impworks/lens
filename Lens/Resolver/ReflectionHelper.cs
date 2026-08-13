using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Lens.Compiler;
using Lens.Translations;
using Lens.Utils;

namespace Lens.Resolver
{
    /// <summary>
    /// A collection is useful tools for working with Reflection entities.
    /// </summary>
    internal static class ReflectionHelper
    {
        #region Various resolvers

        /// <summary>
        /// Resolves an extension method by arguments.
        /// </summary>
        public static MethodWrapper ResolveExtensionMethod(TypeResolutionContext ctx, ExtensionMethodResolver resolver, Type type, string name, Type[] argTypes, Type[] hints, LambdaResolver lambdaResolver)
        {
            var method = resolver.ResolveExtensionMethod(ctx, type, name, argTypes);
            var args = method.GetParameters();
            var info = new MethodWrapper
            {
                Name = name,
                DeclaringType = TypeEntryCache.Of(method.DeclaringType),

                MethodInfo = method,
                IsStatic = true,
                IsVirtual = false,
                ReturnType = TypeEntryCache.Of(method.ReturnType),
                ArgumentTypes = args.Select(p => TypeEntryCache.Of(p.ParameterType)).ToArray(),
                IsPartiallyApplied = IsPartiallyApplied(argTypes),
                IsVariadic = IsVariadic(method),
            };

            if (method.IsGenericMethod)
            {
                var expectedTypes = method.GetParameters().Select(p => p.ParameterType).ToArray();
                var genericDefs = method.GetGenericArguments();

                var extMethodArgs = argTypes.ToList();
                extMethodArgs.Insert(0, type);

                var genericValues = GenericHelper.ResolveMethodGenericsByArgs(
                    ctx,
                    expectedTypes,
                    extMethodArgs.ToArray(),
                    genericDefs,
                    hints,
                    lambdaResolver
                );

                info.GenericArguments = genericValues.Select(TypeEntryCache.Of).ToArray();
                info.MethodInfo = info.MethodInfo.MakeGenericMethod(genericValues);
                info.ReturnType = TypeEntryCache.Of(GenericHelper.ApplyGenericArguments(info.ReturnType.Materialize(), genericDefs, genericValues));
                info.ArgumentTypes = expectedTypes.Select(t => TypeEntryCache.Of(GenericHelper.ApplyGenericArguments(t, genericDefs, genericValues))).ToArray();
            }
            else if (hints != null)
            {
                Error(CompilerMessages.GenericArgsToNonGenericMethod, name);
            }

            return info;
        }

        /// <summary>
        /// Resolves a group of methods by name.
        /// Only non-generic methods are returned!
        /// </summary>
        public static IEnumerable<MethodWrapper> ResolveMethodGroup(TypeResolutionContext ctx, Type type, string name)
        {
            try
            {
                return GetMethodsByName(type, name).Where(m => !m.IsGenericMethod).Select(m => new MethodWrapper(m));
            }
            catch (NotSupportedException)
            {
                if (!type.IsGenericType)
                    throw;

                var genType = type.GetGenericTypeDefinition();
                var genericMethods = GetMethodsByName(genType, name).Where(m => !m.IsGenericMethod).ToArray();

                return genericMethods.Select(
                    m =>
                    {
                        var declType = ResolveActualDeclaringType(ctx, type, m.DeclaringType);
                        return new MethodWrapper
                        {
                            Name = name,
                            DeclaringType = TypeEntryCache.Of(type),

                            MethodInfo = GetMethodVersionForType(declType, m),
                            IsStatic = m.IsStatic,
                            IsVirtual = m.IsVirtual,
                            ArgumentTypes = m.GetParameters().Select(p => TypeEntryCache.Of(GenericHelper.ApplyGenericArguments(p.ParameterType, declType))).ToArray(),
                            ReturnType = TypeEntryCache.Of(GenericHelper.ApplyGenericArguments(m.ReturnType, declType))
                        };
                    }
                );
            }
        }


        /// <summary>
        /// Resolves an indexer property from a type by its argument.
        /// </summary>
        public static MethodWrapper ResolveIndexer(TypeResolutionContext ctx, Type type, Type idxType, bool isGetter)
        {
            if (type is TypeBuilder)
                throw new NotSupportedException();

            try
            {
                var indexer = ResolveIndexerProperty(ctx, type, idxType, isGetter, p => p);
                return new MethodWrapper(indexer);
            }
            catch (NotSupportedException)
            {
                if (!type.IsGenericType)
                    throw;

                var genType = type.GetGenericTypeDefinition();
                var indexer = ResolveIndexerProperty(ctx, genType, idxType, isGetter, p => GenericHelper.ApplyGenericArguments(p, type));
                var declType = ResolveActualDeclaringType(ctx, type, indexer.DeclaringType);

                return new MethodWrapper
                {
                    DeclaringType = TypeEntryCache.Of(type),

                    MethodInfo = GetMethodVersionForType(declType, indexer),
                    IsStatic = false,
                    IsVirtual = indexer.IsVirtual,
                    ArgumentTypes = indexer.GetParameters().Select(p => TypeEntryCache.Of(GenericHelper.ApplyGenericArguments(p.ParameterType, type))).ToArray(),
                    ReturnType = TypeEntryCache.Of(GenericHelper.ApplyGenericArguments(indexer.ReturnType, type))
                };
            }
        }

        /// <summary>
        /// Finds a property that can work as an index.
        /// </summary>
        private static MethodInfo ResolveIndexerProperty(TypeResolutionContext ctx, Type type, Type idxType, bool isGetter, Func<Type, Type> typeProcessor)
        {
            var indexers = new List<Tuple<PropertyInfo, Type, int>>();

            foreach (var pty in type.GetProperties())
            {
                if (isGetter && pty.GetGetMethod() == null)
                    continue;

                if (!isGetter && pty.GetSetMethod() == null)
                    continue;

                var idxArgs = pty.GetIndexParameters();
                if (idxArgs.Length != 1)
                    continue;

                var argType = typeProcessor(idxArgs[0].ParameterType);
                var distance = TypeEntryCache.Of(argType).DistanceFrom(ctx, TypeEntryCache.Of(idxType));

                indexers.Add(new Tuple<PropertyInfo, Type, int>(pty, argType, distance));
            }

            indexers.Sort((x, y) => x.Item3.CompareTo(y.Item3));

            if (indexers.Count == 0 || indexers[0].Item3 == int.MaxValue)
                Error(
                    isGetter
                        ? CompilerMessages.IndexGetterNotFound
                        : CompilerMessages.IndexSetterNotFound,
                    type,
                    idxType
                );

            if (indexers.Count > 1 && indexers[0].Item3 == indexers[1].Item3)
                Error(
                    CompilerMessages.IndexAmbigious,
                    type,
                    indexers[0].Item2,
                    indexers[1].Item2,
                    Environment.NewLine
                );

            var it = indexers[0];

            return isGetter 
                ? it.Item1.GetGetMethod()
                : it.Item1.GetSetMethod();
        }

        /// <summary>
        /// Resolves the best-matching method-like entity within a generic list, with both sides of
        /// the comparison given as entries.
        ///
        /// The declared signature of a member the script declares is made of entries already, and
        /// some of them - the generic parameters of a method whose builders do not exist yet - stand
        /// for no CLR type at all, so the overload above cannot be used for those.
        /// </summary>
        /// <typeparam name="T">Type of method-like entity.</typeparam>
        /// <param name="list">List of method-like entitites.</param>
        /// <param name="argsGetter">A function that gets method entity arguments.</param>
        /// <param name="argTypes">Desired argument types.</param>
        public static MethodLookupResult<T> ResolveMethodByArgs<T>(TypeResolutionContext ctx, IEnumerable<T> list, Func<T, TypeEntry[]> argsGetter, Func<T, bool> isVariadicGetter, TypeEntry[] argTypes)
        {
            var result = list.Select(x => TypeExtensions.ArgumentDistance(ctx, argTypes, argsGetter(x), x, isVariadicGetter(x)))
                             .OrderBy(rec => rec.Distance)
                             .Take(2) // no more than 2 is needed
                             .ToArray();

            if (result.Length == 0 || result[0].Distance == int.MaxValue)
                throw new KeyNotFoundException();

            if (result.Length == 2 && result[0].Distance == result[1].Distance)
                throw new AmbiguousMatchException();

            return result[0];
        }

        #endregion

        #region Entry-space generic inference

        /// <summary>
        /// Infers the values of a generic method's type parameters from the call site, entirely in
        /// the entry model.
        ///
        /// The counterpart of <see cref="GenericHelper.ResolveMethodGenericsByArgs"/> for a call
        /// whose arguments are made of something the script declared: those have no CLR type to
        /// match against until the declaration has been emitted, and asking for one is what would
        /// force the assembly into existence.
        ///
        /// Lambda inference is deliberately absent. A lambda reaches here already resolved into a
        /// delegate type, because that is the only shape in which it can be matched structurally.
        /// </summary>
        public static TypeEntry[] InferGenericArguments(TypeResolutionContext ctx, TypeEntry[] parameters, TypeEntry[] expectedTypes, TypeEntry[] actualTypes, TypeEntry[] hints)
        {
            if (hints != null && hints.Length != parameters.Length)
                throw new ArgumentException(nameof(hints));

            var values = new TypeEntry[parameters.Length];

            if (hints != null)
                for (var idx = 0; idx < hints.Length; idx++)
                    values[idx] = hints[idx];

            var count = Math.Min(expectedTypes.Length, actualTypes.Length);
            for (var idx = 0; idx < count; idx++)
                Unify(ctx, expectedTypes[idx], actualTypes[idx], parameters, values);

            for (var idx = 0; idx < values.Length; idx++)
                if (ReferenceEquals(values[idx], null))
                    throw new TypeMatchException(string.Format(CompilerMessages.GenericArgumentNotResolved, parameters[idx]));

            return values;
        }

        /// <summary>
        /// Matches an expected signature against the type actually passed, recording whatever the
        /// match says about the parameters being inferred.
        /// </summary>
        private static void Unify(TypeResolutionContext ctx, TypeEntry expected, TypeEntry actual, TypeEntry[] parameters, TypeEntry[] values)
        {
            if (ReferenceEquals(expected, null) || ReferenceEquals(actual, null))
                return;

            if (expected.IsGenericParameter)
            {
                for (var idx = 0; idx < parameters.Length; idx++)
                    if (TypeEntry.Same(parameters[idx], expected) && ReferenceEquals(values[idx], null))
                        values[idx] = actual;

                return;
            }

            if (expected.IsArray || expected.IsByRef)
            {
                Unify(ctx, expected.ElementType, actual.ElementType, parameters, values);
                return;
            }

            if (!expected.IsGenericType || expected.IsGenericTypeDefinition)
                return;

            // the argument need not be the expected generic type itself: passing a List<T> where an
            // IEnumerable<> is expected is what makes inference worth doing at all
            var source = FindInstantiationOf(ctx, expected.GetGenericDefinition(), actual);
            if (ReferenceEquals(source, null))
                return;

            var expectedArgs = expected.GenericArguments;
            var actualArgs = source.GenericArguments;

            var pairs = Math.Min(expectedArgs.Length, actualArgs.Length);
            for (var idx = 0; idx < pairs; idx++)
                Unify(ctx, expectedArgs[idx], actualArgs[idx], parameters, values);
        }

        /// <summary>
        /// Finds the instantiation of a generic definition that a type is, inherits from or
        /// implements.
        /// </summary>
        private static TypeEntry FindInstantiationOf(TypeResolutionContext ctx, TypeEntry definition, TypeEntry type)
        {
            if (ReferenceEquals(definition, null))
                return null;

            foreach (var curr in type.SelfAndBaseTypes())
                if (TypeEntry.Same(curr.GetGenericDefinition(), definition))
                    return curr;

            foreach (var curr in type.GetInterfaces(ctx))
                if (TypeEntry.Same(curr.GetGenericDefinition(), definition))
                    return curr;

            return null;
        }

        #endregion

        #region Delegate handling

        /// <summary>
        /// Gets the information about a delegate by its type.
        /// </summary>
        public static MethodWrapper WrapDelegate(TypeResolutionContext ctx, Type type)
        {
            if (!type.IsCallableType())
                throw new ArgumentException("type");

            return ResolveMethodGroup(ctx, type, "Invoke").Single();
        }

        /// <summary>
        /// Checks if two delegates can be combined.
        /// </summary>
        public static bool CanCombineDelegates(TypeResolutionContext ctx, Type left, Type right)
        {
            if (!left.IsCallableType() || !right.IsCallableType())
                return false;

            var rt = WrapDelegate(ctx, left).ReturnType;
            var args = WrapDelegate(ctx, right).ArgumentTypes;

            return args.Count() == 1 && args[0].Materialize().IsAssignableFrom(rt.Materialize());
        }

        /// <summary>
        /// Creates a new delegate that combines the two given ones.
        /// </summary>
        public static Type CombineDelegates(TypeResolutionContext ctx, Type left, Type right)
        {
            if (!left.IsCallableType() || !right.IsCallableType())
                return null;

            var args = WrapDelegate(ctx, left).ArgumentTypes;
            var rt = WrapDelegate(ctx, right).ReturnType;

            return FunctionalHelper.CreateDelegateType(rt.Materialize(), args.Select(x => x.Materialize()).ToArray());
        }

        #endregion

        #region Checkers

        /// <summary>
        /// Checks if method can accept an arbitrary amount of arguments.
        /// </summary>
        public static bool IsVariadic(MethodBase method)
        {
            var args = method.GetParameters();
            return args.Length > 0 && args[args.Length - 1].IsDefined(typeof(ParamArrayAttribute), true);
        }

        /// <summary>
        /// Checks if the list of argument types denotes a partial application case.
        /// </summary>
        public static bool IsPartiallyApplied(Type[] argTypes)
        {
            return argTypes.Contains(typeof(UnspecifiedType));
        }

        /// <summary>
        /// Checks if the list of argument types denotes a partial application case.
        /// </summary>
        public static bool IsPartiallyApplied(TypeEntry[] argTypes)
        {
            return argTypes.Any(x => x.Is<UnspecifiedType>());
        }

        /// <summary>
        /// Checks if the possibly generic type has a default constructor.
        /// </summary>
        public static bool HasDefaultConstructor(this Type type)
        {
            if (type.IsValueType)
                return true;

            try
            {
                return type.GetConstructor(Type.EmptyTypes) != null;
            }
            catch (NotSupportedException)
            {
                if (type.IsGenericType)
                    return type.GetGenericTypeDefinition().HasDefaultConstructor();

                // arrays do not have constructors
                if (type.IsArray)
                    return false;

                // type labels and records have constructors
                return true;
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Returns the list of methods by name, flattening interface hierarchy.
        /// </summary>
        public static IEnumerable<MethodInfo> GetMethodsByName(Type type, string name)
        {
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy;

            var result = type.GetMethods(flags).Where(m => m.Name == name).ToArray();
            if (type.IsInterface && !result.Any())
                result = type.GetInterfaces()
                             .Select(x => x.IsGenericType ? x.GetGenericTypeDefinition() : x)
                             .SelectMany(x => GetMethodsByName(x, name))
                             .ToArray();

            return WithoutHiddenMethods(result);
        }

        /// <summary>
        /// Drops the base declarations that a derived type has replaced.
        ///
        /// Reflection reports both, because the signature that hides is the same signature that was
        /// hidden. Overload resolution would then find two candidates that fit the call equally
        /// well and give up - which is what Task&lt;T&gt;.GetAwaiter, hiding Task.GetAwaiter with a
        /// different return type, does to anything that tries to await a task.
        /// </summary>
        private static MethodInfo[] WithoutHiddenMethods(MethodInfo[] methods)
        {
            if (methods.Length < 2)
                return methods;

            return methods.Where(m => !methods.Any(other => Hides(other, m))).ToArray();
        }

        /// <summary>
        /// Checks whether one declaration replaces another: same shape, declared further down.
        /// </summary>
        private static bool Hides(MethodInfo derived, MethodInfo hidden)
        {
            if (ReferenceEquals(derived, hidden) || derived.DeclaringType == null || hidden.DeclaringType == null)
                return false;

            if (derived.DeclaringType == hidden.DeclaringType || !hidden.DeclaringType.IsAssignableFrom(derived.DeclaringType))
                return false;

            if (derived.IsStatic != hidden.IsStatic || derived.GetGenericArguments().Length != hidden.GetGenericArguments().Length)
                return false;

            var derivedArgs = derived.GetParameters();
            var hiddenArgs = hidden.GetParameters();
            if (derivedArgs.Length != hiddenArgs.Length)
                return false;

            for (var idx = 0; idx < derivedArgs.Length; idx++)
                if (derivedArgs[idx].ParameterType != hiddenArgs[idx].ParameterType)
                    return false;

            return true;
        }

        /// <summary>
        /// Resolves an "actual" declaring type if generic workaround has been applied to an interface.
        /// </summary>
        /// <param name="type">Actual type</param>
        /// <param name="decl">Declaring generic type of method or property</param>
        public static Type ResolveActualDeclaringType(TypeResolutionContext ctx, Type type, Type decl)
        {
            if (type.IsInterface && type != decl)
            {
                var ifaces = ctx.ResolveInterfaces(type);
                foreach (var curr in ifaces)
                {
                    if (curr == decl || (curr.IsGenericType && decl.IsGenericType && curr.GetGenericTypeDefinition() == decl.GetGenericTypeDefinition()))
                        return curr;
                }
            }

            return type;
        }

        /// <summary>
        /// Creates a generic method version for a specific type.
        /// </summary>
        public static MethodInfo GetMethodVersionForType(Type type, MethodInfo method)
        {
            if (method != null && type.IsGenericType)
                return TypeBuilder.GetMethod(type, method);

            return method;
        }

        /// <summary>
        /// Throws a new error.
        /// </summary>
        [ContractAnnotation("=> halt")]
        [DebuggerStepThrough]
        private static void Error(string msg, params object[] args)
        {
            throw new LensCompilerException(string.Format(msg, args));
        }

        #endregion
    }
}