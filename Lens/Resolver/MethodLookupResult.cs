using System;

namespace Lens.Resolver
{
    /// <summary>
    /// The single result of a method lookup operation.
    /// </summary>
    internal class MethodLookupResult<T>
    {
        #region Constructor

        public MethodLookupResult(T method, int dist, TypeEntry[] args, bool isExpanded = false, int omittedCount = 0)
        {
            Method = method;
            Distance = dist;
            ArgumentTypes = args;
            IsExpanded = isExpanded;
            OmittedCount = omittedCount;
        }

        #endregion

        #region Fields

        /// <summary>
        /// Reference to method (or constructor).
        /// </summary>
        public readonly T Method;

        /// <summary>
        /// Calculated total distance of all arguments.
        /// </summary>
        public readonly int Distance;

        /// <summary>
        /// Inferred or evident argument types.
        /// </summary>
        public readonly TypeEntry[] ArgumentTypes;

        /// <summary>
        /// Whether the candidate only applies because its trailing param array is being built out of
        /// the arguments, rather than passed as one.
        /// </summary>
        public readonly bool IsExpanded;

        /// <summary>
        /// How many of the trailing parameters the call site left out, to be filled in from the
        /// defaults they declare.
        /// </summary>
        public readonly int OmittedCount;

        #endregion
    }
}