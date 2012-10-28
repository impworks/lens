using System;
using System.Linq;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.Operators
{
	/// <summary>
	/// The base for all binary operators.
	/// </summary>
	public abstract class BinaryOperatorNodeBase : OperatorNodeBase
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

		/// <summary>
		/// Available numeric types.
		/// </summary>
		public static readonly Type[] NumericTypes = new[]
		{
			typeof (int),
			typeof (float),
			typeof (long),
			typeof (double)
		};

		/// <summary>
		/// Gets the best-suiting common type for current argument types.
		/// </summary>
		protected Type getResultNumericType(Type leftType, Type rightType)
		{
			if (!NumericTypes.Contains(leftType) || !NumericTypes.Contains(rightType))
				return null;

			if (leftType == rightType)
				return leftType;

			if (leftType == typeof (double) || rightType == typeof (double))
				return typeof (double);

			if (leftType == typeof(float) || rightType == typeof(float))
				return typeof (float);

			if (leftType == typeof(long) || rightType == typeof(long))
				return typeof(long);

			return typeof(int);
		}

		/// <summary>
		/// Displays an error indicating that argument types are wrong.
		/// </summary>
		protected void TypeError(Type left, Type right)
		{
			Error("Cannot apply operator '{0}' to arguments of types '{1}' and '{2}' respectively.", OperatorRepresentation, left, right);
		}

		/// <summary>
		/// Returns the typically calculated argument type or throws an error.
		/// </summary>
		/// <returns></returns>
		protected Type getNumericTypeOrError()
		{
			var left = LeftOperand.GetExpressionType();
			var right = RightOperand.GetExpressionType();

			var numeric = getResultNumericType(left, right);
			if (numeric == null)
				TypeError(left, right);

			return numeric;
		}

		#region Equality members

		protected bool Equals(BinaryOperatorNodeBase other)
		{
			return Equals(LeftOperand, other.LeftOperand) && Equals(RightOperand, other.RightOperand);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((BinaryOperatorNodeBase)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return ((LeftOperand != null ? LeftOperand.GetHashCode() : 0) * 397) ^ (RightOperand != null ? RightOperand.GetHashCode() : 0);
			}
		}

		#endregion
	}
}
