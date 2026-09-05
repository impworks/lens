using System.Collections.Generic;
using System.Linq;
using Lens.Resolver;
using Lens.SyntaxTree.Internals;

namespace Lens.SyntaxTree.Expressions.GetSet
{
    /// <summary>
    /// The base node for accessing array-like structures by index.
    /// </summary>
    internal abstract class IndexNodeBase : AccessorNodeBase
    {
        #region Fields

        /// <summary>
        /// Index expressions, in source order.
        ///
        /// There is more than one of them when a multidimensional array is addressed - 'a[1; 2]' -
        /// or when an indexer that takes several arguments is invoked.
        /// </summary>
        public List<NodeBase> Indexes { get; set; } = new List<NodeBase>();

        /// <summary>
        /// The single index expression, for the overwhelmingly common one-dimensional case.
        /// Assigning it replaces the whole list.
        /// </summary>
        public NodeBase Index
        {
            get => Indexes.Count > 0 ? Indexes[0] : null;
            set => Indexes = new List<NodeBase> {value};
        }

        #endregion

        #region Transform

        /// <summary>
        /// The index list an access spells, followed by the defaults of the trailing index
        /// parameters it leaves out - or null when it leaves none out.
        ///
        /// An indexer is a method in every respect but its spelling, and a default index is filled
        /// in exactly as a default argument is: the access is rewritten into the one that names
        /// every index, and everything after that sees an ordinary access.
        /// </summary>
        protected List<NodeBase> IndexesWithDefaults(MethodWrapper accessor)
        {
            var omitted = accessor?.OmittedArguments;
            if (omitted == null || omitted.Length == 0)
                return null;

            return Indexes.Concat(omitted.Select(x => (NodeBase) new DefaultArgumentNode(x.Value, x.Type))).ToList();
        }

        #endregion

        #region Debug

        protected bool Equals(IndexNodeBase other)
        {
            return Equals(Expression, other.Expression)
                   && Indexes.Count == other.Indexes.Count
                   && Indexes.SequenceEqual(other.Indexes)
                   && IsNullSafe == other.IsNullSafe;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((IndexNodeBase) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (Expression != null ? Expression.GetHashCode() : 0);

                foreach (var curr in Indexes)
                    hashCode = (hashCode * 397) ^ (curr != null ? curr.GetHashCode() : 0);

                hashCode = (hashCode * 397) ^ IsNullSafe.GetHashCode();
                return hashCode;
            }
        }

        #endregion
    }
}