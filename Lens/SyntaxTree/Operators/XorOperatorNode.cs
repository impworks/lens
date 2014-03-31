using Lens.Compiler;

namespace Lens.SyntaxTree.Operators
{
	internal class XorOperatorNode : BinaryOperatorNodeBase
	{
		protected override string OperatorRepresentation
		{
			get { return "^^"; }
		}

		protected override string OverloadedMethodName
		{
			get { return "op_ExclusiveOr"; }
		}

		protected override void compileOperator(Context ctx)
		{
			var gen = ctx.CurrentMethod.Generator;
			loadAndConvertNumerics(ctx);
			gen.EmitXor();
		}

		protected override dynamic unrollConstant(dynamic left, dynamic right)
		{
			return left ^ right;
		}
	}
}
