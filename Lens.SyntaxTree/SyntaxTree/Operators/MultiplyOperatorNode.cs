using System;

namespace Lens.SyntaxTree.SyntaxTree.Operators
{
	/// <summary>
	/// An operator node that multiplies one value by another value.
	/// </summary>
	public class MultiplyOperatorNode : BinaryOperatorNodeBase
	{
		public override string OperatorRepresentation
		{
			get { return "*"; }
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
