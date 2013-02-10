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

		protected override Type resolveExpressionType(Context ctx)
		{
			var type = Operand.GetExpressionType(ctx);

			if(type != typeof(bool))
				TypeError(type);

			return type;
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentILGenerator;

			Operand.Compile(ctx, true);

			gen.EmitConstant(0);
			gen.EmitCompareEqual();
		}
	}
}
