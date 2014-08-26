using System;
using Lens.Compiler;

namespace Lens.SyntaxTree.Operators
{
	/// <summary>
	/// A node representing the boolean inversion operator.
	/// </summary>
	internal class InversionOperatorNode : UnaryOperatorNodeBase
	{
		protected override string OperatorRepresentation
		{
			get { return "not"; }
		}

		protected override Type resolveOperatorType(Context ctx)
		{
			return CastOperatorNode.IsImplicitlyBoolean(Operand.Resolve(ctx)) ? typeof (bool) : null;
		}

		protected override NodeBase expand(Context ctx, bool mustReturn)
		{
			var op = Operand as InversionOperatorNode;
			if (op != null)
				return op.Operand;

			return base.expand(ctx, mustReturn);
		}

		protected override void compileOperator(Context ctx)
		{
			var gen = ctx.CurrentMethod.Generator;

			Expr.Cast<bool>(Operand).Emit(ctx, true);

			gen.EmitConstant(0);
			gen.EmitCompareEqual();
		}

		protected override dynamic unrollConstant(dynamic value)
		{
			return !value;
		}
	}
}
