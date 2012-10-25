using System;
using System.Linq;
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

		/// <summary>
		/// A textual operator representation for error reporting.
		/// </summary>
		public abstract string OperatorRepresentation { get; }

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
		protected static readonly Type[] NumericTypes = new[]
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

			if (checkPair<float, long>(leftType, rightType))
				return typeof(double);

			if (leftType == typeof(float) || rightType == typeof(float))
				return typeof (float);

			if (leftType == typeof(long) || rightType == typeof(long))
				return typeof(long);

			return typeof(int);
		}

		/// <summary>
		/// Checks the types of an argument pair.
		/// </summary>
		private static bool checkPair<T1, T2>(Type left, Type right)
		{
			return (left == typeof (T1) && right == typeof (T2)) || (right == typeof (T1) && left == typeof (T2));
		}

		/// <summary>
		/// Displays an error indicating that argument types are wrong.
		/// </summary>
		protected void TypeError(Type left, Type right)
		{
			Error("Cannot apply operator '{0}' to arguments of types '{1}' and '{2}' respectively.", OperatorRepresentation, left, right);
		}
	}
}
