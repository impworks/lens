using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.Translations;

namespace Lens.SyntaxTree.SyntaxTree.Operators
{
	/// <summary>
	/// An operator node that divides one value by another value.
	/// </summary>
	public class DivideOperatorNode : BinaryOperatorNodeBase
	{
		public override string OperatorRepresentation
		{
			get { return "/"; }
		}

		public override string OverloadedMethodName
		{
			get { return "op_Division"; }
		}

		protected override void compileOperator(Context ctx)
		{
			var gen = ctx.CurrentILGenerator;

			GetExpressionType(ctx);
			loadAndConvertNumerics(ctx);

			gen.EmitDivide();
		}

		protected override dynamic unrollConstant(dynamic left, dynamic right)
		{
			if(left is int && right is int && right == 0)
				Error(CompilerMessages.ConstantDivisionByZero);

			return left/right;
		}
	}
}
