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

		#region Equality members

		protected bool Equals(UnaryOperatorNodeBase other)
		{
			return Equals(Operand, other.Operand);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((UnaryOperatorNodeBase)obj);
		}

		public override int GetHashCode()
		{
			return (Operand != null ? Operand.GetHashCode() : 0);
		}

		#endregion
	}
}
