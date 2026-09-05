using System.Collections.Generic;
using Lens.Compiler;
using Lens.Resolver;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.Expressions.Instantiation
{
    /// <summary>
    /// An index counted from the end of whatever it is applied to: '^1' is the last element.
    ///
    /// The node is a System.Index and nothing more - it does not know what it will be applied to,
    /// and cannot: the same value indexes a string of one length and an array of another. Where it
    /// is written directly inside an index access, the access lowers it into arithmetic over the
    /// length it does know, and no Index is built at all.
    /// </summary>
    internal class IndexFromEndNode : NodeBase
    {
        #region Fields

        /// <summary>
        /// How far from the end the index points: 1 is the last element.
        /// </summary>
        public NodeBase Operand { get; set; }

        #endregion

        #region Resolve

        protected override TypeEntry ResolveInternal(Context ctx, bool mustReturn)
        {
            var type = Operand.Resolve(ctx);
            var intType = TypeEntryCache.Of<int>();

            if (!intType.IsExtendablyAssignableFrom(ctx.Resolver, type))
                Error(Operand, CompilerMessages.ImplicitCastImpossible, type, intType);

            return ctx.ResolveType(RangeTypes.IndexTypeName);
        }

        #endregion

        #region Transform

        protected override NodeBase Expand(Context ctx, bool mustReturn)
        {
            return RangeTypes.NewIndex(Operand, true);
        }

        internal override IEnumerable<NodeChild> GetChildren()
        {
            yield return new NodeChild(Operand);
        }

        internal override IReadOnlyList<NodeBase> Operands => new[] {Operand};

        internal override NodeBase WithOperands(IReadOnlyList<NodeBase> operands)
        {
            var copy = Copy<IndexFromEndNode>();
            copy.Operand = operands[0];
            return copy;
        }

        #endregion

        #region Debug

        protected bool Equals(IndexFromEndNode other)
        {
            return Equals(Operand, other.Operand);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((IndexFromEndNode) obj);
        }

        public override int GetHashCode()
        {
            return Operand != null ? Operand.GetHashCode() : 0;
        }

        public override string ToString()
        {
            return string.Format("fromend({0})", Operand);
        }

        #endregion
    }
}
