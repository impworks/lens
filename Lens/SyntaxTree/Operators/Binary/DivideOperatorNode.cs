using Lens.Compiler;
using Lens.Resolver;
using Lens.Translations;

namespace Lens.SyntaxTree.Operators.Binary
{
	/// <summary>
	/// An operator node that divides one value by another value.
	/// </summary>
	internal class DivideOperatorNode : BinaryOperatorNodeBase
	{
		#region Operator basics

		protected override string OperatorRepresentation => "/";

	    protected override string OverloadedMethodName => "op_Division";

	    protected override BinaryOperatorNodeBase RecreateSelfWithArgs(NodeBase left, NodeBase right)
	    {
	        return new DivideOperatorNode {LeftOperand = left, RightOperand = right};
	    }

	    #endregion

		#region Transform

		protected override NodeBase Expand(Context ctx, bool mustReturn)
		{
			if (RightOperand.IsConstant && RightOperand.ConstantValue == 1)
				return LeftOperand;

			return base.Expand(ctx, mustReturn);
		}

		#endregion

		#region Emit

		protected override void EmitOperator(Context ctx)
		{
			LoadAndConvertNumerics(ctx);
			ctx.CurrentMethod.Generator.EmitDivide();
		}

		#endregion

		#region Constant unroll

		protected override dynamic UnrollConstant(dynamic left, dynamic right)
		{
			var leftType = left.GetType();
			var rightType = right.GetType();

			if(TypeExtensions.IsIntegerType(leftType) && TypeExtensions.IsIntegerType(rightType) && right == 0)
				Error(CompilerMessages.ConstantDivisionByZero);

			return left/right;
		}

		#endregion
	}
}
