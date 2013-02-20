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

		protected override Type resolveExpressionType(Context ctx, bool mustReturn = true)
		{
			var type = Operand.GetExpressionType(ctx);

			if(!CastOperatorNode.IsImplicitlyBoolean(type))
				TypeError(type);

			return type;
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentILGenerator;

			CastOperatorNode.CompileAsBoolean(Operand, ctx);

			gen.EmitConstant(0);
			gen.EmitCompareEqual();
		}
	}
}
