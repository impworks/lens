using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Lens.Utils
{
    /// <summary>
    /// Compares objects by identity, ignoring any Equals override they may have.
    ///
    /// The side tables of the binder are keyed by node, and syntax tree nodes compare structurally:
    /// two occurrences of the literal '1' in a script are equal to each other but are separate
    /// expressions with separate binding results.
    /// </summary>
    internal class ReferenceEqualityComparer<T> : IEqualityComparer<T>
        where T : class
    {
        public static readonly ReferenceEqualityComparer<T> Instance = new ReferenceEqualityComparer<T>();

        private ReferenceEqualityComparer()
        {
        }

        public bool Equals(T x, T y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(T obj)
        {
            return obj == null ? 0 : RuntimeHelpers.GetHashCode(obj);
        }
    }
}
