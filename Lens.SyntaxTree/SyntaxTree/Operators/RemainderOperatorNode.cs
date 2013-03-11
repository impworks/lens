using Lens.SyntaxTree.Compiler;

namespace Lens.SyntaxTree.SyntaxTree.Operators
{
	/// <summary>
	/// An operator node that divides one value by another value.
	/// </summary>
	public class RemainderOperatorNode : BinaryOperatorNodeBase
	{
		public override string OperatorRepresentation
		{
			get { return "%"; }
		}

		public override string OverloadedMethodName
		{
			get { return "op_Modulus"; }
		}

		protected override void compileOperator(Context ctx)
		{
			var gen = ctx.CurrentILGenerator;
			GetExpressionType(ctx);
			loadAndConvertNumerics(ctx);
			gen.EmitRemainder();
		}
	}
}
