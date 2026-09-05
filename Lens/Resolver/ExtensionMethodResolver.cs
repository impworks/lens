using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Lens.Resolver
{
    /// <summary>
    /// Finds a list of possible extension methods for a given type.
    /// </summary>
    internal class ExtensionMethodResolver
    {
        #region Constructors

        public ExtensionMethodResolver(Dictionary<string, bool> namespaces, ReferencedAssemblyCache asmCache)
        {
            _cache = new Dictionary<TypeEntry, Dictionary<string, List<MethodInfo>>>();
            _namespaces = namespaces;
            _asmCache = asmCache;
        }

        #endregion

        #region Fields

        /// <summary>
        /// Extension method cache for faster lookup.
        /// It is scoped to the current compilation, because the set of namespaces it depends on is.
        /// </summary>
        private readonly Dictionary<TypeEntry, Dictionary<string, List<MethodInfo>>> _cache;

        /// <summary>
        /// Namespaces where the types containing extension methods are looked for.
        /// </summary>
        private readonly Dictionary<string, bool> _namespaces;

        /// <summary>
        /// List of referenced assemblies.
        /// </summary>
        private readonly ReferencedAssemblyCache _asmCache;

        #endregion

        #region Methods

        /// <summary>
        /// Gets an extension method by given arguments.
        /// </summary>
        public MethodInfo ResolveExtensionMethod(TypeResolutionContext ctx, TypeEntry type, string name, TypeEntry[] args, out int omittedCount)
        {
            if (!_cache.ContainsKey(type))
                _cache.Add(type, FindMethodsForType(ctx, type));

            if (!_cache[type].ContainsKey(name))
                throw new KeyNotFoundException();

            var methods = _cache[type][name];

            // the receiver is a parameter like any other here, so that the tie between Queryable's
            // overload and Enumerable's - which differ in the receiver above all - can be decided
            var applicable = methods.Where(m => m.Name == name)
                                    .Select(mi => Weigh(ctx, mi, type, args))
                                    .Where(c => c.Distance != int.MaxValue)
                                    .ToArray();

            if (applicable.Length == 0)
                throw new KeyNotFoundException();

            var receiverAndArgs = new[] {type}.Concat(args).ToArray();
            var best = TypeExtensions.BestCandidates(ctx, receiverAndArgs, applicable, c => c.Distance, c => c.ArgumentTypes);

            if (best.Length > 1)
                throw new AmbiguousMatchException();

            omittedCount = best[0].OmittedCount;
            return best[0].Method;
        }

        /// <summary>
        /// Returns every extension method applicable to a type, grouped by name.
        /// Completion needs the whole set, where a call site only ever needs one.
        /// </summary>
        public Dictionary<string, List<MethodInfo>> EnumerateExtensionMethods(TypeResolutionContext ctx, TypeEntry type)
        {
            if (!_cache.ContainsKey(type))
                _cache.Add(type, FindMethodsForType(ctx, type));

            return _cache[type];
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Returns the list of extension methods for given type.
        /// </summary>
        private Dictionary<string, List<MethodInfo>> FindMethodsForType(TypeResolutionContext ctx, TypeEntry forType)
        {
            var dict = new Dictionary<string, List<MethodInfo>>();

            foreach (var asm in _asmCache.Assemblies)
            {
                if (asm.IsDynamic)
                    continue;

                try
                {
                    var types = asm.GetExportedTypes();
                    foreach (var type in types)
                    {
                        if (!type.IsSealed || type.IsGenericType || !type.IsDefined(typeof(ExtensionAttribute), false))
                            continue;

                        if (type.Namespace == null || !_namespaces.ContainsKey(type.Namespace))
                            continue;

                        var methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public);
                        foreach (var method in methods)
                        {
                            if (!method.IsDefined(typeof(ExtensionAttribute), false))
                                continue;

                            var argType = method.GetParameters()[0].ParameterType;
                            if (!TypeEntryCache.Of(argType).IsExtendablyAssignableFrom(ctx, forType))
                                continue;

                            if (!dict.ContainsKey(method.Name))
                                dict[method.Name] = new List<MethodInfo>();

                            dict[method.Name].Add(method);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }
            }

            return dict;
        }

        /// <summary>
        /// Weighs one extension method against the call, receiver included.
        /// </summary>
        private static Candidate Weigh(TypeResolutionContext ctx, MethodInfo method, TypeEntry type, TypeEntry[] args)
        {
            var signature = Signature(method);
            var parameters = method.GetParameters();

            // the receiver is parameter zero and is always supplied, so what the call may leave out
            // is counted among the parameters after it
            var declared = parameters.Length - 1;
            var omitted = declared - args.Length;

            if (omitted > 0 && omitted > ReflectionHelper.OptionalArgumentCount(parameters))
                return new Candidate(method, int.MaxValue, signature, 0);

            var baseDist = signature[0].DistanceFrom(ctx, type);
            var argsDist = TypeExtensions.TypeListDistance(ctx, args, signature.Skip(1).Take(args.Length));

            if (baseDist == int.MaxValue || argsDist == int.MaxValue)
                return new Candidate(method, int.MaxValue, signature, 0);

            // a call that leaves an argument out is a worse match than one that spells it
            return new Candidate(method, baseDist + argsDist + Math.Max(omitted, 0), signature, Math.Max(omitted, 0));
        }

        /// <summary>
        /// The declared signature of an extension method, receiver included.
        /// </summary>
        private static TypeEntry[] Signature(MethodInfo method)
        {
            return method.GetParameters().Select(p => TypeEntryCache.Of(p.ParameterType)).ToArray();
        }

        #endregion

        #region Nested classes

        /// <summary>
        /// One extension method considered for a call.
        /// </summary>
        private class Candidate
        {
            public Candidate(MethodInfo method, int distance, TypeEntry[] argumentTypes, int omittedCount)
            {
                Method = method;
                Distance = distance;
                ArgumentTypes = argumentTypes;
                OmittedCount = omittedCount;
            }

            public readonly MethodInfo Method;
            public readonly int Distance;
            public readonly TypeEntry[] ArgumentTypes;

            /// <summary>
            /// How many of the trailing parameters the call leaves out.
            /// </summary>
            public readonly int OmittedCount;
        }

        #endregion
    }
}