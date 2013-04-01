using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.SyntaxTree.Literals;
using Lens.SyntaxTree.Translations;
using Lens.SyntaxTree.Utils;

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
			if(LeftOperand.GetExpressionType(ctx).IsIntegerType() && RightOperand is IntNode && (RightOperand as IntNode).Value == 0)
				Error(CompilerMessages.ConstantDivisionByZero);
			
			var gen = ctx.CurrentILGenerator;

			GetExpressionType(ctx);
			loadAndConvertNumerics(ctx);

			gen.EmitDivide();
		}
	}
}
