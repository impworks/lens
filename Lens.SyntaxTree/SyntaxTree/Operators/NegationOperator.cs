using System;
using System.Linq;

namespace Lens.SyntaxTree.SyntaxTree.Operators
{
	/// <summary>
	/// A node representing the unary numeric negation operation.
	/// </summary>
	public class NegationOperator : UnaryOperatorNodeBase
	{
		public override string OperatorRepresentation
		{
			get { return "-"; }
		}

		public override Type GetExpressionType()
		{
			var type = Operand.GetExpressionType();

			if(!BinaryOperatorNodeBase.NumericTypes.Contains(type))
				TypeError(type);

			return type;
		}

		public override void Compile()
		{
			throw new NotImplementedException();
		}
	}
}
