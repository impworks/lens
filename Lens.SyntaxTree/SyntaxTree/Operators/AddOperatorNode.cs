using System;

namespace Lens.SyntaxTree.SyntaxTree.Operators
{
	/// <summary>
	/// An operator node that adds two values together.
	/// </summary>
	public class AddOperatorNode : BinaryOperatorNodeBase
	{
		public override string OperatorRepresentation
		{
			get { return "+"; }
		}

		public override Type GetExpressionType()
		{
			var left = LeftOperand.GetExpressionType();
			var right = RightOperand.GetExpressionType();

			if (left == typeof (string) && left == right)
				return typeof (string);

			var numeric = getResultNumericType(left, right);
			if (numeric == null)
				TypeError(left, right);

			return numeric;
		}

		public override void Compile()
		{
			throw new NotImplementedException();
		}
	}
}
