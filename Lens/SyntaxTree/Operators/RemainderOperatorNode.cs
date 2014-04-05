using Lens.Compiler;

namespace Lens.SyntaxTree.Operators
{
	/// <summary>
	/// An operator node that divides one value by another value.
	/// </summary>
	internal class RemainderOperatorNode : BinaryOperatorNodeBase
	{
		protected override string OperatorRepresentation
		{
			get { return "%"; }
		}

		protected override string OverloadedMethodName
		{
			get { return "op_Modulus"; }
		}

		protected override void compileOperator(Context ctx)
		{
			loadAndConvertNumerics(ctx);
			ctx.CurrentMethod.Generator.EmitRemainder();
		}

		protected override dynamic unrollConstant(dynamic left, dynamic right)
		{
			return left % right;
		}
	}
}
