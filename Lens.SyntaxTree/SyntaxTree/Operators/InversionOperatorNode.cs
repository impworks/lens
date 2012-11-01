using System;

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

		public override Type GetExpressionType()
		{
			var type = Operand.GetExpressionType();

			if(type != typeof(bool))
				TypeError(type);

			return type;
		}

		public override void Compile()
		{
			throw new NotImplementedException();
		}
	}
}
