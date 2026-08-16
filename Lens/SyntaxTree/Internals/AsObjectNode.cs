using System.Collections.Generic;
using Lens.Compiler;
using Lens.Resolver;
using Lens.Utils;

namespace Lens.SyntaxTree.Internals
{
    /// <summary>
    /// Produces the value of an expression as an object, and null when the expression has no value.
    ///
    /// This is what the script's own value is made of. A script body is not a function: its last
    /// statement may produce anything or nothing at all, and either way the script answers object.
    /// The root method has always handled that at emission, as two special cases keyed on being the
    /// root; a script that awaits has its body moved into a state machine, where the value is handed
    /// to a completion source instead and those special cases no longer apply.
    ///
    /// The decision cannot be taken when the machine is built, because nothing has been resolved
    /// yet - and it must not be, because lowering rewrites 'await x' into a call that may well be
    /// void. So it is taken here, once the tree is final.
    /// </summary>
    internal class AsObjectNode : NodeBase
    {
        #region Constructor

        public AsObjectNode(NodeBase expression)
        {
            Expression = expression;
        }

        #endregion

        #region Fields

        /// <summary>
        /// The expression whose value is wanted, whether or not it has one.
        /// </summary>
        public readonly NodeBase Expression;

        #endregion

        #region Resolve

        protected override TypeEntry ResolveInternal(Context ctx, bool mustReturn)
        {
            Expression.Resolve(ctx);

            return TypeEntryCache.Of<object>();
        }

        #endregion

        #region Transform

        internal override IEnumerable<NodeChild> GetChildren()
        {
            yield return new NodeChild(Expression, true);
        }

        internal override IReadOnlyList<NodeBase> Operands => new[] {Expression};

        internal override NodeBase WithOperands(IReadOnlyList<NodeBase> operands)
        {
            return new AsObjectNode(operands[0]) {StartLocation = StartLocation, EndLocation = EndLocation};
        }

        #endregion

        #region Emit

        protected override void EmitInternal(Context ctx, bool mustReturn)
        {
            var gen = ctx.CurrentMethod.Generator;
            var type = Expression.Resolve(ctx);

            if (type.IsVoid())
            {
                // the statement still runs: it is the last thing the script does, and only its value
                // is missing
                Expression.Emit(ctx, false);
                gen.EmitNull();
                return;
            }

            Expression.Emit(ctx, true);

            if (type.IsValueType)
                gen.EmitBox(type.Materialize());
        }

        #endregion

        #region Debug

        public override string ToString()
        {
            return $"object({Expression})";
        }

        #endregion
    }
}
