using Lens.Compiler;
using Lens.Translations;
using Lens.Resolver;

namespace Lens.SyntaxTree.Operators
{
	/// <summary>
	/// An operator node that divides one value by another value.
	/// </summary>
	internal class DivideOperatorNode : BinaryOperatorNodeBase
	{
		#region Operator basics

		protected override string OperatorRepresentation
		{
			get { return "/"; }
		}

		protected override string OverloadedMethodName
		{
			get { return "op_Division"; }
		}

		#endregion

		#region Transform

		protected override NodeBase expand(Context ctx, bool mustReturn)
		{
			if (RightOperand.IsConstant && RightOperand.ConstantValue == 1)
				return LeftOperand;

			return base.expand(ctx, mustReturn);
		}

		#endregion

		#region Emit

		protected override void emitOperator(Context ctx)
		{
			loadAndConvertNumerics(ctx);
			ctx.CurrentMethod.Generator.EmitDivide();
		}

		#endregion

		#region Constant unroll

		protected override dynamic unrollConstant(dynamic left, dynamic right)
		{
			var leftType = left.GetType();
			var rightType = right.GetType();

			if(TypeExtensions.IsIntegerType(leftType) && TypeExtensions.IsIntegerType(rightType) && right == 0)
				error(CompilerMessages.ConstantDivisionByZero);

			return left/right;
		}

		#endregion
	}
}
