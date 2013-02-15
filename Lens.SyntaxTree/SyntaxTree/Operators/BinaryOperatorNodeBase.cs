using System;
using System.Collections.Generic;
using Lens.SyntaxTree.Compiler;
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
			set { LocationSetError(); }
		}

		public override LexemLocation EndLocation
		{
			get { return RightOperand.EndLocation; }
			set { LocationSetError(); }
		}

		public override IEnumerable<NodeBase> GetChildNodes()
		{
			yield return LeftOperand;
			yield return RightOperand;
		}
		
		/// <summary>
		/// Displays an error indicating that argument types are wrong.
		/// </summary>
		protected void TypeError(Type left, Type right)
		{
			Error("Cannot apply operator '{0}' to arguments of types '{1}' and '{2}' respectively.", OperatorRepresentation, left, right);
		}

		/// <summary>
		/// Resolves a common numeric type.
		/// </summary>
		protected Type resolveNumericType(Context ctx)
		{
			var left = LeftOperand.GetExpressionType(ctx);
			var right = RightOperand.GetExpressionType(ctx);

			if (left.IsNumericType() && right.IsNumericType())
			{
				var type = TypeExtensions.GetNumericOperationType(left, right);
				if(type == null)
					Error("Cannot apply apply math operations to arguments of different signedness.");

				return type;
			}

			TypeError(left, right);
			return null;
		}

		/// <summary>
		/// Loads both arguments and converts them to the biggest common type.
		/// </summary>
		protected void loadAndConvertNumerics(Context ctx)
		{
			var gen = ctx.CurrentILGenerator;

			var left = LeftOperand.GetExpressionType(ctx);
			var right = RightOperand.GetExpressionType(ctx);
			var type = TypeExtensions.GetNumericOperationType(left, right);

			LeftOperand.Compile(ctx, true);
			if (left != type)
				gen.EmitConvert(type);

			RightOperand.Compile(ctx, true);
			if (right != type)
				gen.EmitConvert(type);
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

		public override string ToString()
		{
			return string.Format("op{0}({1}, {2})", OperatorRepresentation, LeftOperand, RightOperand);
		}
	}
}
