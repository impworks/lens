using System;

namespace Lens.SyntaxTree.SyntaxTree.Operators
{
	/// <summary>
	/// A node representing object comparison operations.
	/// </summary>
	public class ComparisonOperatorNode : BinaryOperatorNodeBase
	{
		public ComparisonOperatorNode(ComparisonOperatorKind kind = default(ComparisonOperatorKind))
		{
			Kind = kind;
		}

		/// <summary>
		/// The kind of equality operator.
		/// </summary>
		public ComparisonOperatorKind Kind { get; set; }

		public override string OperatorRepresentation
		{
			get
			{
				switch (Kind)
				{
					case ComparisonOperatorKind.Equals: return "==";
					case ComparisonOperatorKind.NotEquals: return "<>";
					case ComparisonOperatorKind.Less: return "<";
					case ComparisonOperatorKind.LessEquals: return "<=";
					case ComparisonOperatorKind.Greater: return ">";
					case ComparisonOperatorKind.GreaterEquals: return ">=";

					default: throw new ArgumentException("Comparison operator kind is invalid!");
				}
			}
		}

		public override Type GetExpressionType()
		{
			var left = LeftOperand.GetExpressionType();
			var right = RightOperand.GetExpressionType();

			if (left != typeof(bool) || right != typeof(bool))
				TypeError(left, right);

			return typeof(bool);
		}

		public override void Compile()
		{
			throw new NotImplementedException();
		}

		#region Equality members

		protected bool Equals(ComparisonOperatorNode other)
		{
			return base.Equals(other) && Kind == other.Kind;
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((ComparisonOperatorNode)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return (base.GetHashCode() * 397) ^ (int)Kind;
			}
		}

		#endregion
	}

	/// <summary>
	/// The kind of comparison operators.
	/// </summary>
	public enum ComparisonOperatorKind
	{
		Equals,
		NotEquals,
		Less,
		LessEquals,
		Greater,
		GreaterEquals
	}
}
