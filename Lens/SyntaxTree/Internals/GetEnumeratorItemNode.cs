using System.Collections.Generic;
using Lens.Compiler;
using Lens.Resolver;
using Lens.Utils;

namespace Lens.SyntaxTree.Internals
{
    /// <summary>
    /// Reads the current item of an enumerator, narrowed to the type the sequence actually holds.
    ///
    /// The lowering pass runs before anything is bound, so it cannot tell whether the enumerator it
    /// opened is a typed one or the untyped one a multidimensional array hands out - and only the
    /// second needs its items unwrapped. This node carries the question until binding can answer
    /// it, exactly as <see cref="GetEnumeratorNode"/> does for the enumerator itself.
    /// </summary>
    internal class GetEnumeratorItemNode : NodeBase
    {
        #region Constructor

        public GetEnumeratorItemNode(NodeBase iterator, NodeBase sequence)
        {
            _iterator = iterator;
            _sequence = sequence;
        }

        #endregion

        #region Fields

        private readonly NodeBase _iterator;

        /// <summary>
        /// The sequence the enumerator was opened over. It is only ever asked for its type: the
        /// expression itself belongs to the statement that opened the enumerator, and is emitted
        /// exactly once, there.
        /// </summary>
        private readonly NodeBase _sequence;

        #endregion

        #region Resolve

        protected override TypeEntry ResolveInternal(Context ctx, bool mustReturn)
        {
            var current = Expr.GetMember(_iterator, "Current").Resolve(ctx);
            var seqType = _sequence.Resolve(ctx);

            // a rank > 1 array is only a bare IEnumerable, but it still knows what it holds
            return seqType.IsArray && !seqType.IsVectorArray ? seqType.ElementType : current;
        }

        #endregion

        #region Transform

        internal override IEnumerable<NodeChild> GetChildren()
        {
            yield return new NodeChild(_iterator);
        }

        protected override NodeBase Expand(Context ctx, bool mustReturn)
        {
            var current = Expr.GetMember(_iterator, "Current");
            var itemType = Resolve(ctx);

            return current.Resolve(ctx).Equals(itemType) ? current : Expr.Cast(current, itemType);
        }

        #endregion

        #region Debug

        public override string ToString()
        {
            return $"current({_iterator})";
        }

        #endregion
    }
}
