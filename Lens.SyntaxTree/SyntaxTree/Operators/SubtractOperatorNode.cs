using System;

namespace Lens.SyntaxTree.SyntaxTree.Operators
{
	/// <summary>
	/// An operator node that subtracts a value from another value.
	/// </summary>
	public class SubtractOperatorNode : BinaryOperatorNodeBase
	{
		public override string OperatorRepresentation
		{
			get { return "-"; }
		}

		public override Type GetExpressionType()
		{
			return getNumericTypeOrError();
		}

		public override void Compile()
		{
			throw new NotImplementedException();
		}
	}
}
