using System;
using Lens.Compiler;

namespace Lens.SyntaxTree.Operators
{
	/// <summary>
	/// A node representing the boolean inversion operator.
	/// </summary>
	internal class InversionOperatorNode : UnaryOperatorNodeBase
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

		protected override dynamic unrollConstant(dynamic value)
		{
			return !value;
		}
	}
}
