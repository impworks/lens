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
        public static MethodWrapper ResolveIndexer(TypeResolutionContext ctx, Type type, Type[] idxTypes, bool isGetter)
        {
            if (type is TypeBuilder)
                throw new NotSupportedException();

            try
            {
                var indexer = ResolveIndexerProperty(ctx, type, idxTypes, isGetter, p => p);
                return new MethodWrapper(indexer);
            }
            catch (NotSupportedException)
            {
                if (!type.IsGenericType)
                    throw;

                var genType = type.GetGenericTypeDefinition();
                var indexer = ResolveIndexerProperty(ctx, genType, idxTypes, isGetter, p => GenericHelper.ApplyGenericArguments(p, type));
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
        private static MethodInfo ResolveIndexerProperty(TypeResolutionContext ctx, Type type, Type[] idxTypes, bool isGetter, Func<Type, Type> typeProcessor)
        {
            var indexers = new List<Tuple<PropertyInfo, Type[], int>>();
            var passed = idxTypes.Select(TypeEntryCache.Of).ToArray();

            foreach (var pty in type.GetProperties())
            {
                if (isGetter && pty.GetGetMethod() == null)
                    continue;

                if (!isGetter && pty.GetSetMethod() == null)
                    continue;

                var idxArgs = pty.GetIndexParameters();
                if (idxArgs.Length != idxTypes.Length)
                    continue;

                var argTypes = idxArgs.Select(p => typeProcessor(p.ParameterType)).ToArray();

                // the same summed distance overload resolution uses for methods: an indexer of
                // several arguments is a method in every respect but its spelling
                var distance = TypeExtensions.TypeListDistance(ctx, passed, argTypes.Select(TypeEntryCache.Of));

                indexers.Add(new Tuple<PropertyInfo, Type[], int>(pty, argTypes, distance));
            }

            indexers.Sort((x, y) => x.Item3.CompareTo(y.Item3));

            if (indexers.Count == 0 || indexers[0].Item3 == int.MaxValue)
                Error(
                    isGetter
                        ? CompilerMessages.IndexGetterNotFound
                        : CompilerMessages.IndexSetterNotFound,
                    type,
                    string.Join("; ", idxTypes.Select(x => x.ToString()))
                );

            if (indexers.Count > 1 && indexers[0].Item3 == indexers[1].Item3)
                Error(
                    CompilerMessages.IndexAmbigious,
                    type,
                    string.Join("; ", indexers[0].Item2.Select(x => x.ToString())),
                    string.Join("; ", indexers[1].Item2.Select(x => x.ToString())),
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
            var applicable = list.Select(x => TypeExtensions.ArgumentDistance(ctx, argTypes, argsGetter(x), x, isVariadicGetter(x)))
                                 .Where(rec => rec.Distance != int.MaxValue)
                                 .ToArray();

            if (applicable.Length == 0)
                throw new KeyNotFoundException();

            var best = TypeExtensions.BestCandidates(ctx, argTypes, applicable, rec => rec.Distance, rec => rec.ArgumentTypes, rec => rec.IsExpanded);
            if (best.Length > 1)
                throw new AmbiguousMatchException();

            return best[0];
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
        /// A lambda whose argument types the call site left out is matched through
        /// <paramref name="lambdaResolver"/>: the parameter says what the lambda takes, the resolver
        /// says what it then returns, and the return type is what a parameter such as the TKey of
        /// OrderByDescending can only be read off.
        /// </summary>
        public static TypeEntry[] InferGenericArguments(TypeResolutionContext ctx, TypeEntry[] parameters, TypeEntry[] expectedTypes, TypeEntry[] actualTypes, TypeEntry[] hints, EntryLambdaResolver lambdaResolver = null)
        {
            if (hints != null && hints.Length != parameters.Length)
                throw new ArgumentException(nameof(hints));

            var values = new TypeEntry[parameters.Length];

            if (hints != null)
                for (var idx = 0; idx < hints.Length; idx++)
                    values[idx] = hints[idx];

            var count = Math.Min(expectedTypes.Length, actualTypes.Length);
            for (var idx = 0; idx < count; idx++)
                Unify(ctx, expectedTypes[idx], actualTypes[idx], parameters, values, lambdaResolver, idx);

            for (var idx = 0; idx < values.Length; idx++)
                if (ReferenceEquals(values[idx], null))
                    throw new TypeMatchException(string.Format(CompilerMessages.GenericArgumentNotResolved, parameters[idx]));

            return values;
        }

        /// <summary>
        /// Matches an expected signature against the type actually passed, recording whatever the
        /// match says about the parameters being inferred.
        /// </summary>
        private static void Unify(TypeResolutionContext ctx, TypeEntry expected, TypeEntry actual, TypeEntry[] parameters, TypeEntry[] values, EntryLambdaResolver lambdaResolver = null, int position = -1)
        {
            if (ReferenceEquals(expected, null) || ReferenceEquals(actual, null))
                return;

            // a parameter that wants an expression tree is matched against the delegate the tree
            // stands for, which is the shape the argument arrives in
            if (expected.IsExpressionType() && !actual.IsExpressionType())
                expected = expected.UnwrapExpressionType();

            // a lambda written without argument types is not a delegate yet, so there is nothing to
            // match structurally: the parameter has to say what it takes before it can say anything
            if (actual.IsLambdaType() && expected.IsCallableType())
            {
                UnifyLambda(ctx, expected, actual, parameters, values, lambdaResolver, position);
                return;
            }

            if (expected.IsGenericParameter)
            {
                for (var idx = 0; idx < parameters.Length; idx++)
                    if (TypeEntry.Same(parameters[idx], expected) && ReferenceEquals(values[idx], null))
                        values[idx] = actual;

                return;
            }

            if (expected.IsArray || expected.IsByRef)
            {
                if (expected.IsArray && (!actual.IsArray || expected.ArrayRank != actual.ArrayRank))
                    return;

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
        /// Reads whatever a lambda argument says about the parameters being inferred.
        ///
        /// The lambda arrives with its own arguments still unspecified, which is exactly why it
        /// cannot be matched against the parameter directly: 'x -> x.Stock' is a shape, not a type.
        /// The parameter supplies the argument types, the resolver then types the body against them,
        /// and the body's type is matched against what the parameter returns - which is the only
        /// place a type argument mentioned solely in the return position can come from.
        /// </summary>
        private static void UnifyLambda(TypeResolutionContext ctx, TypeEntry expected, TypeEntry actual, TypeEntry[] parameters, TypeEntry[] values, EntryLambdaResolver lambdaResolver, int position)
        {
            if (lambdaResolver == null || position < 0)
                return;

            var expectedInfo = WrapDelegate(ctx, expected);
            var actualInfo = WrapDelegate(ctx, actual);

            var argTypes = new TypeEntry[actualInfo.ArgumentTypes.Length];
            var count = Math.Min(expectedInfo.ArgumentTypes.Length, argTypes.Length);

            for (var idx = 0; idx < count; idx++)
            {
                var expectedArg = expectedInfo.ArgumentTypes[idx];
                var actualArg = actualInfo.ArgumentTypes[idx];

                if (actualArg.Is<UnspecifiedType>())
                {
                    // the lambda did not say what it takes, so the parameter does - in the terms of
                    // whatever has been inferred by now, which for an extension method is the
                    // receiver and therefore everything this needs
                    var inferred = ConstructedTypeEntry.SubstituteInto(ctx, expectedArg, parameters, values);

                    if (MentionsUnresolved(inferred, parameters, values))
                        throw new LensCompilerException(string.Format(CompilerMessages.LambdaArgGenericsUnresolved, expectedArg));

                    argTypes[idx] = inferred;
                }
                else
                {
                    argTypes[idx] = actualArg;
                    Unify(ctx, expectedArg, actualArg, parameters, values);
                }
            }

            var returnType = lambdaResolver(position, argTypes);
            Unify(ctx, expectedInfo.ReturnType, returnType, parameters, values);
        }

        /// <summary>
        /// Whether a type still names a parameter whose value has not been worked out.
        /// </summary>
        private static bool MentionsUnresolved(TypeEntry type, TypeEntry[] parameters, TypeEntry[] values)
        {
            if (ReferenceEquals(type, null))
                return false;

            if (type.IsGenericParameter)
            {
                for (var idx = 0; idx < parameters.Length; idx++)
                    if (TypeEntry.Same(parameters[idx], type))
                        return ReferenceEquals(values[idx], null);

                return false;
            }

            if (MentionsUnresolved(type.ElementType, parameters, values))
                return true;

            foreach (var curr in type.GenericArguments)
                if (MentionsUnresolved(curr, parameters, values))
                    return true;

            return false;
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
        public static MethodWrapper WrapDelegate(TypeResolutionContext ctx, TypeEntry type)
        {
            // Expression<TDelegate> describes the same signature as TDelegate does, and everything
            // that asks a delegate for its shape wants that shape whichever of the two was spelled
            type = type.UnwrapExpressionType();

            if (!type.IsCallableType())
                throw new ArgumentException(nameof(type));

            // a delegate made only of host types is one reflection can answer for
            if (!type.ContainsDeclared)
                return WrapDelegate(ctx, type.Materialize());

            // Func<SomeRecord, int> has no CLR type, but Func<,> does: Invoke is read off the
            // definition and its signature rewritten into the instantiation's terms, exactly as a
            // member of any other host instantiation is. Materializing instead would build the
            // record's assembly, which is what analysis exists not to need.
            var definition = type.GetGenericDefinition();
            var invoke = definition?.Materialize().GetMethod("Invoke");
            if (invoke == null)
                throw new ArgumentException(nameof(type));

            var parameters = definition.GenericArguments;
            var arguments = type.GenericArguments;

            return new MethodWrapper
            {
                Name = invoke.Name,
                DeclaringType = type,

                IsStatic = false,
                IsVirtual = invoke.IsVirtual,

                ArgumentTypes = invoke.GetParameters()
                                      .Select(p => ConstructedTypeEntry.SubstituteInto(ctx, TypeEntryCache.Of(p.ParameterType), parameters, arguments))
                                      .ToArray(),
                ReturnType = ConstructedTypeEntry.SubstituteInto(ctx, TypeEntryCache.Of(invoke.ReturnType), parameters, arguments),

                MethodInfoSource = () => GetMethodVersionForType(type.Materialize(), invoke)
            };
        }

        /// <summary>
        /// Returns a wrapper for a delegate type.
        /// </summary>
        public static MethodWrapper WrapDelegate(TypeResolutionContext ctx, Type type)
        {
            // Expression<TDelegate> describes the same signature as TDelegate does, and everything
            // that asks a delegate for its shape - argument inference above all - wants that shape
            // whichever of the two the call site spelled
            type = type.UnwrapExpressionType();

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