using System;
using System.Collections.Generic;
using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.Translations;
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

		protected override Type resolveExpressionType(Context ctx, bool mustReturn = true)
		{
			var leftType = LeftOperand.GetExpressionType(ctx);
			var rightType = RightOperand.GetExpressionType(ctx);

			var result = resolveOperatorType(ctx, leftType, rightType);
			if (result != null)
				return result;

			if (leftType.IsNumericType() && rightType.IsNumericType())
				return resolveNumericType(ctx);

			if (OverloadedMethodName != null)
			{
				try
				{
					m_OverloadedMethod = ctx.ResolveMethod(leftType, OverloadedMethodName, new[] { leftType, rightType });

					// cannot be generic
					if (m_OverloadedMethod != null)
						return m_OverloadedMethod.ReturnType;
				}
				catch { }
			}

			Error(Messages.OperatorBinaryTypesMismatch, OperatorRepresentation, leftType, rightType);
			return null;
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentILGenerator;

			GetExpressionType(ctx);

			if (m_OverloadedMethod == null)
			{
				compileOperator(ctx);
				return;
			}

			var ps = m_OverloadedMethod.ArgumentTypes;
			Expr.Cast(LeftOperand, ps[0]).Compile(ctx, true);
			Expr.Cast(RightOperand, ps[1]).Compile(ctx, true);
			gen.EmitCall(m_OverloadedMethod.MethodInfo);
		}

		protected virtual Type resolveOperatorType(Context ctx, Type leftType, Type rightType)
		{
			return null;
		}

		protected abstract void compileOperator(Context ctx);

		/// <summary>
		/// Resolves a common numeric type.
		/// </summary>
		private Type resolveNumericType(Context ctx)
		{
			var left = LeftOperand.GetExpressionType(ctx);
			var right = RightOperand.GetExpressionType(ctx);

			var type = TypeExtensions.GetNumericOperationType(left, right);
			if(type == null)
				Error(Messages.OperatorTypesSignednessMismatch);

			return type;
		}

		/// <summary>
		/// Loads both arguments and converts them to the biggest common type.
		/// </summary>
		protected void loadAndConvertNumerics(Context ctx, Type type = null)
		{
			var gen = ctx.CurrentILGenerator;

			var left = LeftOperand.GetExpressionType(ctx);
			var right = RightOperand.GetExpressionType(ctx);

			if(type == null)
				type = TypeExtensions.GetNumericOperationType(left, right);

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
