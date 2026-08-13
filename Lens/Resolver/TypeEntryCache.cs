using System;
using System.Runtime.CompilerServices;

namespace Lens.Resolver
{
    /// <summary>
    /// Hands out the one entry that stands for a given CLR type.
    ///
    /// Referential equality matters: the compiler compares types constantly, and two entries for
    /// the same type would make every comparison a structural walk at best and wrong at worst.
    /// Reflection interns runtime types and <see cref="TypeResolutionContext.MakeGenericType"/>
    /// interns the instantiations the compiler builds, so keying on the identity of the CLR type is
    /// enough to be canonical here.
    ///
    /// The table is weak-keyed and shared: a wrapper around a host type holds no per-compilation
    /// state, so there is nothing to scope to a context and nothing to leak once a builder is
    /// collected.
    /// </summary>
    internal static class TypeEntryCache
    {
        #region Fields

        private static readonly ConditionalWeakTable<Type, TypeEntry> Entries = new ConditionalWeakTable<Type, TypeEntry>();

        private static readonly ConditionalWeakTable<Type, TypeEntry>.CreateValueCallback Factory =
            type => new ReflectedTypeEntry(type);

        #endregion

        #region Methods

        /// <summary>
        /// The entry that stands for a host-provided type.
        /// </summary>
        public static TypeEntry Of(Type type)
        {
            return type == null ? null : Entries.GetValue(type, Factory);
        }

        /// <summary>
        /// The entry that stands for a host-provided type named at compile time.
        /// </summary>
        public static TypeEntry Of<T>()
        {
            return Of(typeof(T));
        }

        /// <summary>
        /// The entries that stand for a list of host-provided types.
        /// </summary>
        public static TypeEntry[] Of(Type[] types)
        {
            if (types == null)
                return null;

            var result = new TypeEntry[types.Length];
            for (var idx = 0; idx < types.Length; idx++)
                result[idx] = Of(types[idx]);

            return result;
        }

        /// <summary>
        /// Registers the entry that stands for a type the script declared, so that a CLR type
        /// arriving from elsewhere resolves back to the declaration rather than to a bare wrapper.
        /// </summary>
        public static void Register(Type type, TypeEntry entry)
        {
            if (type == null || entry == null)
                return;

            Entries.Remove(type);
            Entries.Add(type, entry);
        }

        #endregion
    }
}
