using Lens.SyntaxTree.Compiler;

namespace Lens.SyntaxTree.SyntaxTree.Operators
{
	/// <summary>
	/// An operator node that multiplies one value by another value.
	/// </summary>
	public class MultiplyOperatorNode : BinaryOperatorNodeBase
	{
		public override string OperatorRepresentation
		{
			get { return "*"; }
		}

		public override string OverloadedMethodName
		{
			get { return "op_Multiply"; }
		}

		protected override void compileOperator(Context ctx)
		{
			var gen = ctx.CurrentILGenerator;
			GetExpressionType(ctx);
			loadAndConvertNumerics(ctx);
			gen.EmitMultiply();
		}
	}
}
