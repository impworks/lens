using System.Collections.Generic;
using Lens.Compiler;
using Lens.Resolver;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.Expressions.Instantiation
{
    /// <summary>
    /// A range of indices: 'a..b', and any of the two bounds may be left out.
    ///
    /// Either bound is an integer counted from the start or an index counted from the end, and the
    /// node is a System.Range whichever they are. As with a single index, an access that has a
    /// range written directly inside it lowers the two bounds into arithmetic over the length it
    /// knows, and no Range is built.
    ///
    /// A 'for' header spells its two ends the same way, and that is not this node: the loop is over
    /// the numbers between them and knows both of them already, so the parser hands the header its
    /// bounds rather than a value.
    /// </summary>
    internal class RangeNode : NodeBase
    {
        #region Fields

        /// <summary>
        /// The lower bound, or null when it was left out and the range starts at the beginning.
        /// </summary>
        public NodeBase Start { get; set; }

        /// <summary>
        /// The upper bound, exclusive, or null when it was left out and the range runs to the end.
        /// </summary>
        public NodeBase End { get; set; }

        #endregion

        #region Resolve

        protected override TypeEntry ResolveInternal(Context ctx, bool mustReturn)
        {
            CheckBound(ctx, Start);
            CheckBound(ctx, End);

            return ctx.ResolveType(RangeTypes.RangeTypeName);
        }

        /// <summary>
        /// Checks that a bound is something a range can be built out of.
        /// </summary>
        private void CheckBound(Context ctx, NodeBase bound)
        {
            if (bound == null)
                return;

            var type = bound.Resolve(ctx);
            if (RangeTypes.IsIndex(type))
                return;

            var intType = TypeEntryCache.Of<int>();
            if (!intType.IsExtendablyAssignableFrom(ctx.Resolver, type))
                Error(bound, CompilerMessages.ImplicitCastImpossible, type, intType);
        }

        #endregion

        #region Transform

        protected override NodeBase Expand(Context ctx, bool mustReturn)
        {
            return RangeTypes.NewRange(
                IndexOf(ctx, Start, false),
                IndexOf(ctx, End, true)
            );
        }

        /// <summary>
        /// Turns a bound into the index the range is built from.
        /// </summary>
        private static NodeBase IndexOf(Context ctx, NodeBase bound, bool isEnd)
        {
            if (bound == null)
                return isEnd ? RangeTypes.IndexOfEnd() : RangeTypes.IndexOfStart();

            return RangeTypes.IsIndex(bound.Resolve(ctx))
                ? bound
                : RangeTypes.NewIndex(bound, false);
        }

        internal override IEnumerable<NodeChild> GetChildren()
        {
            if (Start != null)
                yield return new NodeChild(Start);

            if (End != null)
                yield return new NodeChild(End);
        }

        internal override IReadOnlyList<NodeBase> Operands
        {
            get
            {
                if (Start != null && End != null)
                    return new[] {Start, End};

                if (Start != null)
                    return new[] {Start};

                return End != null ? new[] {End} : NoOperands;
            }
        }

        internal override NodeBase WithOperands(IReadOnlyList<NodeBase> operands)
        {
            var copy = Copy<RangeNode>();

            var idx = 0;
            if (Start != null)
                copy.Start = operands[idx++];

            if (End != null)
                copy.End = operands[idx];

            return copy;
        }

        #endregion

        #region Debug

        protected bool Equals(RangeNode other)
        {
            return Equals(Start, other.Start) && Equals(End, other.End);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((RangeNode) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Start != null ? Start.GetHashCode() : 0;
                hashCode = (hashCode * 397) ^ (End != null ? End.GetHashCode() : 0);
                return hashCode;
            }
        }

        public override string ToString()
        {
            return string.Format("range({0}..{1})", Start, End);
        }

        #endregion
    }
}
