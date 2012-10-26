using System;

namespace Lens.SyntaxTree.SyntaxTree.Operators
{
	/// <summary>
	/// The base for all unary operators.
	/// </summary>
	public abstract class UnaryOperatorNodeBase : OperatorNodeBase
	{
		/// <summary>
		/// The operand.
		/// </summary>
		public NodeBase Operand { get; set; }

		/// <summary>
		/// Displays an error indicating that argument types are wrong.
		/// </summary>
		protected void TypeError(Type type)
		{
			Error("Cannot apply operator '{0}' to argument of type '{1}'.", OperatorRepresentation, type);
		}
	}
}
