using System;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.Operators
{
	/// <summary>
	/// The base for all binary operators.
	/// </summary>
	public abstract class BinaryOperatorNodeBase : NodeBase
	{
		/// <summary>
		/// The operand to the left side.
		/// </summary>
		public NodeBase LeftOperand { get; set; }
		
		/// <summary>
		/// The operand to the right side.
		/// </summary>
		public NodeBase RightOperand { get; set; }

		public override LexemLocation StartLocation
		{
			get { return LeftOperand.StartLocation; }
			set { throw new InvalidOperationException("Binary operator's locations are determined by operands!"); }
		}

		public override LexemLocation EndLocation
		{
			get { return RightOperand.EndLocation; }
			set { throw new InvalidOperationException("Binary operator's locations are determined by operands!"); }
		}
	}
}
