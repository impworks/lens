using System;
using Lens.SyntaxTree.Compiler;

namespace Lens.SyntaxTree.SyntaxTree.Operators
{
	/// <summary>
	/// A node representing the boolean inversion operator.
	/// </summary>
	public class InversionOperatorNode : UnaryOperatorNodeBase
	{
		public override string OperatorRepresentation
		{
			get { return "not"; }
		}

		protected override Type resolveOperatorType(Context ctx)
		{
			return CastOperatorNode.IsImplicitlyBoolean(Operand.GetExpressionType(ctx)) ? typeof (bool) : null;
		}

		protected override void compileOperator(Context ctx)
		{
			var gen = ctx.CurrentILGenerator;

			Expr.Cast(Operand, typeof(bool)).Compile(ctx, true);

			gen.EmitConstant(0);
			gen.EmitCompareEqual();
		}
	}
}
