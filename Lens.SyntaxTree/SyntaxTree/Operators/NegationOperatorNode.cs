using System;
using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.Operators
{
	/// <summary>
	/// A node representing the unary numeric negation operation.
	/// </summary>
	public class NegationOperatorNode : UnaryOperatorNodeBase
	{
		public override string OperatorRepresentation
		{
			get { return "-"; }
		}

		public override string OverloadedMethodName
		{
			get { return "op_UnaryNegation"; }
		}

		protected override Type resolveExpressionType(Context ctx, bool mustReturn = true)
		{
			var type = Operand.GetExpressionType(ctx);
			return type.IsNumericType() ? type : null;
		}

		protected override void compileOperator(Context ctx)
		{
			var gen = ctx.CurrentILGenerator;

			Operand.Compile(ctx, true);
			gen.EmitNegate();
		}
	}
}
