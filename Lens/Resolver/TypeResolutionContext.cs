using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using Lens.Compiler.Entities;

namespace Lens.Resolver
{
    /// <summary>
    /// Per-compilation state for type resolution: memoization caches and the set of
    /// generic parameters that are currently in scope.
    ///
    /// Type compatibility is not a global property once generics enter the picture: the
    /// distance between 'T' and 'int' depends on the constraints declared for 'T' by its
    /// owning function or type. Therefore all caches live here, next to the generic
    /// environment, and are scoped to a single compilation.
    /// </summary>
    internal class TypeResolutionContext
    {
        #region Fields

        /// <summary>
        /// Memoized results of distance calculation between two types.
        /// </summary>
        private readonly Dictionary<Tuple<Type, Type, bool>, int> _distanceCache = new Dictionary<Tuple<Type, Type, bool>, int>();

        /// <summary>
        /// Memoized lists of interfaces implemented by a type.
        /// </summary>
        private readonly Dictionary<Type, Type[]> _interfaceCache = new Dictionary<Type, Type[]>();

        /// <summary>
        /// The stack of generic parameter sets that are currently in scope.
        /// LENS has no nested declarations, so this stack is at most one frame deep for
        /// user code; compiler-generated types (closures, pure caches) push their own frame.
        /// </summary>
        private readonly List<Dictionary<string, GenericParameterEntity>> _genericScopes = new List<Dictionary<string, GenericParameterEntity>>();

        /// <summary>
        /// The constraint model for every generic parameter declared in LENS code,
        /// keyed by the builder that represents it in the emitted assembly.
        /// </summary>
        private readonly Dictionary<Type, GenericParameterEntity> _knownParameters = new Dictionary<Type, GenericParameterEntity>();

        /// <summary>
        /// Canonical instantiations of generic types that involve entities which are still being
        /// built. Reflection.Emit returns a fresh object from every MakeGenericType call on a
        /// TypeBuilder, and those objects compare unequal to each other, so the compiler has to
        /// hand out one shared instance per instantiation.
        /// </summary>
        private readonly Dictionary<Type, List<Tuple<Type[], Type>>> _instantiations = new Dictionary<Type, List<Tuple<Type[], Type>>>();

        #endregion

        #region Generic type instantiation

        /// <summary>
        /// Applies type arguments to a generic type definition, returning the same object every
        /// time the same instantiation is requested.
        /// </summary>
        public Type MakeGenericType(Type definition, Type[] arguments)
        {
            // runtime types are canonical already
            if (IsStable(definition) && arguments.All(IsStable))
                return definition.MakeGenericType(arguments);

            if (!_instantiations.TryGetValue(definition, out var known))
            {
                known = new List<Tuple<Type[], Type>>();
                _instantiations.Add(definition, known);
            }

            foreach (var curr in known)
                if (curr.Item1.SequenceEqual(arguments))
                    return curr.Item2;

            var result = definition.MakeGenericType(arguments);
            known.Add(new Tuple<Type[], Type>(arguments, result));
            return result;
        }

        #endregion

        #region Distance cache

        /// <summary>
        /// Returns the memoized distance between two types, calculating it if needed.
        /// Types that are still being built are never cached, because their shape can change.
        /// </summary>
        public int CachedDistance(Type varType, Type exprType, bool exactly, Func<int> calculate)
        {
            if (!IsStable(varType) || !IsStable(exprType))
                return calculate();

            var key = new Tuple<Type, Type, bool>(varType, exprType, exactly);
            if (_distanceCache.TryGetValue(key, out var cached))
                return cached;

            var result = calculate();
            _distanceCache[key] = result;
            return result;
        }

        #endregion

        #region Interface cache

        /// <summary>
        /// Gets interfaces of a possibly generic type.
        /// </summary>
        public Type[] ResolveInterfaces(Type type)
        {
            if (IsStable(type) && _interfaceCache.TryGetValue(type, out var cached))
                return cached.ToArray();

            var ifaces = FindInterfaces(type);

            if (IsStable(type))
                _interfaceCache[type] = ifaces;

            return ifaces.ToArray();
        }

        /// <summary>
        /// Actually looks up the interfaces of a type.
        /// </summary>
        private Type[] FindInterfaces(Type type)
        {
            // a LENS-declared generic parameter cannot be asked about its constraints while it
            // is being built, so the constraint model is the only source of truth for it
            var entity = FindConstraints(type);
            if (entity != null)
                return CollectConstraintInterfaces(entity);

            Type[] ifaces;
            try
            {
                ifaces = type.GetInterfaces();
            }
            catch (NotSupportedException)
            {
                if (type.IsGenericType)
                {
                    ifaces = type.GetGenericTypeDefinition().GetInterfaces();
                    for (var idx = 0; idx < ifaces.Length; idx++)
                    {
                        var curr = ifaces[idx];
                        if (curr.IsGenericType)
                            ifaces[idx] = GenericHelper.ApplyGenericArguments(curr, type);
                    }
                }

                else if (type.IsArray)
                {
                    // replace interfaces of any array with element type
                    var elem = type.GetElementType();
                    ifaces = typeof(int[]).GetInterfaces();
                    for (var idx = 0; idx < ifaces.Length; idx++)
                    {
                        var curr = ifaces[idx];
                        if (curr.IsGenericType)
                            ifaces[idx] = curr.GetGenericTypeDefinition().MakeGenericType(elem);
                    }
                }

                // just a built-in type : no interfaces
                else
                {
                    ifaces = Type.EmptyTypes;
                }
            }

            return ifaces;
        }

        /// <summary>
        /// Builds the transitive set of interfaces available on a LENS-declared generic parameter:
        /// the interface constraints themselves, everything they inherit, and whatever the base
        /// type constraint implements.
        /// </summary>
        private Type[] CollectConstraintInterfaces(GenericParameterEntity entity)
        {
            var result = new List<Type>();

            foreach (var currIface in entity.Interfaces)
            {
                var iface = currIface.Materialize();

                if (!result.Contains(iface))
                    result.Add(iface);

                foreach (var curr in ResolveInterfaces(iface))
                    if (!result.Contains(curr))
                        result.Add(curr);
            }

            if (entity.BaseType != null)
            {
                foreach (var curr in ResolveInterfaces(entity.BaseType.Materialize()))
                    if (!result.Contains(curr))
                        result.Add(curr);
            }

            return result.ToArray();
        }

        #endregion

        #region Generic environment

        /// <summary>
        /// Pushes a set of generic parameters into scope.
        /// </summary>
        public void EnterGenericScope(IEnumerable<GenericParameterEntity> parameters)
        {
            var frame = new Dictionary<string, GenericParameterEntity>();
            if (parameters != null)
            {
                foreach (var curr in parameters)
                {
                    frame[curr.Name] = curr;
                    Register(curr);
                }
            }

            _genericScopes.Add(frame);
        }

        /// <summary>
        /// Pops the topmost set of generic parameters.
        /// </summary>
        public void ExitGenericScope()
        {
            if (_genericScopes.Count == 0)
                throw new InvalidOperationException("No generic scope to exit!");

            _genericScopes.RemoveAt(_genericScopes.Count - 1);
        }

        /// <summary>
        /// Finds a generic parameter that is currently in scope by its LENS name.
        /// </summary>
        public GenericParameterEntity FindTypeParameter(string name)
        {
            for (var idx = _genericScopes.Count - 1; idx >= 0; idx--)
                if (_genericScopes[idx].TryGetValue(name, out var found))
                    return found;

            return null;
        }

        /// <summary>
        /// Remembers the constraint model of a LENS-declared generic parameter,
        /// so that it can be found later by its builder.
        /// </summary>
        public void Register(GenericParameterEntity entity)
        {
            if (entity.Builder != null)
            {
                _knownParameters[entity.Builder] = entity;

                // a builder arriving back from reflection must resolve to the declared parameter and
                // not to a bare wrapper, or the same T would have two entries
                TypeEntryCache.Register(entity.Builder, entity.TypeInfo);
            }
        }

        /// <summary>
        /// Checks whether a type is a generic parameter declared in LENS code, as opposed to an
        /// unsubstituted parameter that leaked in from an imported generic definition.
        /// </summary>
        public bool IsDeclaredTypeParameter(Type type)
        {
            return type != null && type.IsGenericParameter && FindConstraints(type) != null;
        }

        /// <summary>
        /// Checks whether a type is a generic parameter declared in LENS code, as opposed to an
        /// unsubstituted parameter that leaked in from an imported generic definition.
        /// </summary>
        public bool IsDeclaredTypeParameter(TypeEntry type)
        {
            return type is GenericParameterEntry;
        }

        /// <summary>
        /// Returns the constraint model for a LENS-declared generic parameter, or null if the type
        /// is not one of ours. The entry carries its own model, so nothing has to be looked up.
        /// </summary>
        public GenericParameterEntity FindConstraints(TypeEntry type)
        {
            return (type as GenericParameterEntry)?.Entity;
        }

        /// <summary>
        /// Returns the constraint model for a LENS-declared generic parameter, or null
        /// if the type is not one of ours.
        /// </summary>
        public GenericParameterEntity FindConstraints(Type type)
        {
            if (type == null)
                return null;

            return _knownParameters.TryGetValue(type, out var found) ? found : null;
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Checks whether a type's shape can no longer change, and therefore whether
        /// answers about it may be memoized.
        /// </summary>
        public static bool IsStable(Type type)
        {
            if (type == null)
                return true;

            if (type is TypeBuilder || type is GenericTypeParameterBuilder)
                return false;

            if (type.IsArray || type.IsByRef || type.IsPointer)
                return IsStable(type.GetElementType());

            if (type.IsGenericType)
            {
                foreach (var curr in type.GetGenericArguments())
                    if (!IsStable(curr))
                        return false;
            }

            return true;
        }

        #endregion
    }
}
