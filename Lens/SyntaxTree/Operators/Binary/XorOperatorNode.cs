using System;
using Lens.Compiler;
using Lens.Resolver;

namespace Lens.SyntaxTree.Operators.Binary
{
    internal class XorOperatorNode : BinaryOperatorNodeBase
    {
        #region Operator basics

        protected override string OperatorRepresentation => "^^";

        protected override string OverloadedMethodName => "op_ExclusiveOr";

        protected override BinaryOperatorNodeBase RecreateSelfWithArgs(NodeBase left, NodeBase right)
        {
            return new XorOperatorNode {LeftOperand = left, RightOperand = right};
        }

        #endregion

        #region Resolve

        protected override Type ResolveOperatorType(Context ctx, Type leftType, Type rightType)
        {
            return leftType == typeof(bool) && rightType == typeof(bool) ? typeof(bool) : null;
        }

        #endregion

        #region Emit

        protected override void EmitOperator(Context ctx)
        {
            var gen = ctx.CurrentMethod.Generator;

            if (LeftOperand.Resolve(ctx).IsNumericType())
            {
                LoadAndConvertNumerics(ctx);
            }
            else
            {
                LeftOperand.Emit(ctx, true);
                RightOperand.Emit(ctx, true);
            }

            gen.EmitXor();
        }

        #endregion

        #region Constant unroll

        protected override dynamic UnrollConstant(dynamic left, dynamic right)
        {
            return left ^ right;
        }

        #endregion
    }
}