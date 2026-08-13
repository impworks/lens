using System;
using System.Collections.Generic;
using Lens.Compiler;
using Lens.Resolver;
using Lens.Utils;

namespace Lens.SyntaxTree.Internals
{
    /// <summary>
    /// Disposes an expression if its type turns out to be disposable, and does nothing otherwise.
    ///
    /// A lowered foreach has to close the enumerator it opened, and whether there is anything to
    /// close is a question only binding can answer: IEnumerator&lt;T&gt; is always disposable, the
    /// non-generic IEnumerator usually is not.
    /// </summary>
    internal class DisposeNode : NodeBase, IMetaNode
    {
        #region Constructor

        public DisposeNode(NodeBase expression)
        {
            Expression = expression;
        }

        #endregion

        #region Fields

        public readonly NodeBase Expression;

        #endregion

        #region Transform

        internal override IEnumerable<NodeChild> GetChildren()
        {
            yield return new NodeChild(Expression);
        }

        protected override NodeBase Expand(Context ctx, bool mustReturn)
        {
            var type = Expression.Resolve(ctx);

            if (!type.Implements(ctx.Resolver, TypeEntryCache.Of<IDisposable>(), false))
                return Expr.Unit();

            return Expr.Invoke(
                Expr.Cast(Expression, typeof(IDisposable)),
                "Dispose"
            );
        }

        #endregion

        #region Debug

        public override string ToString()
        {
            return $"dispose({Expression})";
        }

        #endregion
    }
}
