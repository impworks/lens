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

		protected override Type resolveExpressionType(Context ctx, bool mustReturn = true)
		{
			var type = Operand.GetExpressionType(ctx);

			if(!type.IsNumericType())
				TypeError(type);

			return type;
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentILGenerator;

			Operand.Compile(ctx, true);
			gen.EmitNegate();
		}
	}
}
