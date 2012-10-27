using System;

namespace Lens.SyntaxTree.SyntaxTree.Operators
{
	/// <summary>
	/// An operator node that raises one value to the power of another value.
	/// </summary>
	public class PowOperatorNode : BinaryOperatorNodeBase
	{
		public override string OperatorRepresentation
		{
			get { return "**"; }
		}

		public override Type GetExpressionType()
		{
			return typeof (double);
		}

		public override void Compile()
		{
			throw new NotImplementedException();
		}
	}
}
