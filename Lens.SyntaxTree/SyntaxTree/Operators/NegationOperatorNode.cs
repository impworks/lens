using System;
using System.Linq;
using Lens.SyntaxTree.Compiler;

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

		protected override Type resolveExpressionType(Context ctx)
		{
			var type = Operand.GetExpressionType(ctx);

			if(!BinaryOperatorNodeBase.NumericTypes.Contains(type))
				TypeError(type);

			return type;
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			throw new NotImplementedException();
		}
	}
}
