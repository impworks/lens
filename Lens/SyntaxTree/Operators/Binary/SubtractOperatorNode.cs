using System;
using Lens.Compiler;
using Lens.Resolver;

namespace Lens.SyntaxTree.Operators.Binary
{
    /// <summary>
    /// An operator node that subtracts a value from another value.
    /// </summary>
    internal class SubtractOperatorNode : BinaryOperatorNodeBase
    {
        #region Operator basics

        protected override string OperatorRepresentation => "-";

        protected override string OverloadedMethodName => "op_Subtraction";

        protected override BinaryOperatorNodeBase RecreateSelfWithArgs(NodeBase left, NodeBase right)
        {
            return new SubtractOperatorNode {LeftOperand = left, RightOperand = right};
        }

        #endregion

        #region Resolve

        protected override TypeEntry ResolveOperatorType(Context ctx, TypeEntry leftType, TypeEntry rightType)
        {
            return leftType.Is<string>() && rightType.Is<string>() ? TypeEntryCache.Of<string>() : null;
        }

        #endregion

        #region Transform

        protected override NodeBase Expand(Context ctx, bool mustReturn)
        {
            // folding gives the best code there is, but it can be switched off - and an operator
            // whose meaning is a call rather than an opcode has to become that call either way
            var folded = base.Expand(ctx, mustReturn);
            if (folded != null)
                return folded;

            if (Resolve(ctx).Is<string>())
                return Expr.Invoke(LeftOperand, "Replace", RightOperand, Expr.Str(""));

            return null;
        }

        #endregion

        #region Emit

        protected override void EmitOperator(Context ctx)
        {
            LoadAndConvertNumerics(ctx);
            ctx.CurrentMethod.Generator.EmitSubtract();
        }

        #endregion

        #region Constant unroll

        protected override dynamic UnrollConstant(dynamic left, dynamic right)
        {
            if (left is string && right is string)
                return left.Replace(right, "");

            return left - right;
        }

        #endregion
    }
}