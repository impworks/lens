using System;
using Lens.Compiler;
using Lens.Resolver;

namespace Lens.SyntaxTree.Operators.Unary
{
    /// <summary>
    /// A node representing the unary numeric negation operation.
    /// </summary>
    internal class NegationOperatorNode : UnaryOperatorNodeBase
    {
        #region Operator basics

        protected override string OperatorRepresentation => "-";

        protected override string OverloadedMethodName => "op_UnaryNegation";

        #endregion

        #region Resolve

        protected override Type ResolveOperatorType(Context ctx)
        {
            var type = Operand.Resolve(ctx);
            return type.IsNumericType() ? type : null;
        }

        #endregion

        #region Transform

        protected override NodeBase Expand(Context ctx, bool mustReturn)
        {
            // double negation
            var op = Operand as NegationOperatorNode;
            if (op != null)
                return op.Operand;

            return base.Expand(ctx, mustReturn);
        }

        #endregion

        #region Emit

        protected override void EmitOperator(Context ctx)
        {
            Operand.Emit(ctx, true);
            ctx.CurrentMethod.Generator.EmitNegate();
        }

        #endregion

        #region Constant unroll

        protected override dynamic UnrollConstant(dynamic value)
        {
            return -value;
        }

        #endregion
    }
}