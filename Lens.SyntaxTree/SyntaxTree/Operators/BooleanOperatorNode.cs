using System;

namespace Lens.SyntaxTree.SyntaxTree.Operators
{
	/// <summary>
	/// A node representing AND, OR or XOR binary operations.
	/// </summary>
	public class BooleanOperatorNode : BinaryOperatorNodeBase
	{
		/// <summary>
		/// The kind of boolean operator.
		/// </summary>
		public BooleanOperatorKind Kind { get; set; }

		public override string OperatorRepresentation
		{
			get
			{
				switch (Kind)
				{
					case BooleanOperatorKind.And: return "&&";
					case BooleanOperatorKind.Or: return "||";
					case BooleanOperatorKind.Xor: return "^^";

					default: throw new ArgumentException("Boolean operator kind is invalid!");
				}
			}
		}

		public override Type GetExpressionType()
		{
			var left = LeftOperand.GetExpressionType();
			var right = RightOperand.GetExpressionType();

			if(left != typeof(bool) || right != typeof(bool))
				TypeError(left, right);

			return typeof (bool);
		}

		public override void Compile()
		{
			throw new NotImplementedException();
		}
	}

	/// <summary>
	/// The kind of boolean operators.
	/// </summary>
	public enum BooleanOperatorKind
	{
		And,
		Or,
		Xor
	}
}
