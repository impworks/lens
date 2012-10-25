using System;

namespace Lens.SyntaxTree.SyntaxTree.Operators
{
	/// <summary>
	/// An operator node that subtracts a value from another value.
	/// </summary>
	class SubtractOperatorNode : BinaryOperatorNodeBase
	{
		public override string OperatorRepresentation
		{
			get { return "-"; }
		}

		public override Type GetExpressionType()
		{
			var left = LeftOperand.GetExpressionType();
			var right = RightOperand.GetExpressionType();

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
