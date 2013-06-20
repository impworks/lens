using Lens.SyntaxTree.Compiler;

namespace Lens.SyntaxTree.SyntaxTree.Operators
{
	/// <summary>
	/// An operator node that subtracts a value from another value.
	/// </summary>
	public class SubtractOperatorNode : BinaryOperatorNodeBase
	{
		public override string OperatorRepresentation
		{
			get { return "-"; }
		}

		public override string OverloadedMethodName
		{
			get { return "op_Subtraction"; }
		}

		protected override void compileOperator(Context ctx)
		{
			var gen = ctx.CurrentILGenerator;
			GetExpressionType(ctx);
			loadAndConvertNumerics(ctx);
			gen.EmitSubtract();
		}

		protected override dynamic unrollConstant(dynamic left, dynamic right)
		{
			return left - right;
		}
	}
}
