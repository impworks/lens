using System;
using System.Collections.Generic;
using Lens.Compiler;
using Lens.Resolver;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.Operators
{
	/// <summary>
	/// The base for all binary operators.
	/// </summary>
	internal abstract class BinaryOperatorNodeBase : OperatorNodeBase
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
		/// Checks if numeric type casting checks should be applied to operands.
		/// </summary>
		protected virtual bool IsNumericOperator { get { return true; } }

		protected override Type resolve(Context ctx, bool mustReturn)
		{
			var leftType = LeftOperand.Resolve(ctx);
			var rightType = RightOperand.Resolve(ctx);

			var result = resolveOperatorType(ctx, leftType, rightType);
			if (result != null)
				return result;

			if (OverloadedMethodName != null)
			{
				try
				{
					m_OverloadedMethod = ctx.ResolveMethod(leftType, OverloadedMethodName, new[] { leftType, rightType });
				}
				catch
				{
					try
					{
						m_OverloadedMethod = ctx.ResolveMethod(rightType, OverloadedMethodName, new[] { leftType, rightType });
					}
					catch { }
				}

				// cannot be generic
				if (m_OverloadedMethod != null)
					return m_OverloadedMethod.ReturnType;
			}

			if (IsNumericOperator)
			{
				if (leftType.IsNumericType() && rightType.IsNumericType())
				{
					var commonNumericType = TypeExtensions.GetNumericOperationType(leftType, rightType);
					if (commonNumericType == null)
						error(CompilerMessages.OperatorTypesSignednessMismatch);

					return commonNumericType;
				}
			}

			error(this, CompilerMessages.OperatorBinaryTypesMismatch, OperatorRepresentation, leftType, rightType);
			return null;
		}

		protected override void emitCode(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentMethod.Generator;
			if (m_OverloadedMethod == null)
			{
				emitOperator(ctx);
				return;
			}

			var ps = m_OverloadedMethod.ArgumentTypes;
			Expr.Cast(LeftOperand, ps[0]).Emit(ctx, true);
			Expr.Cast(RightOperand, ps[1]).Emit(ctx, true);
			gen.EmitCall(m_OverloadedMethod.MethodInfo);
		}

		/// <summary>
		/// Resolves operator return type, in case it's not an overloaded method call.
		/// </summary>
		protected virtual Type resolveOperatorType(Context ctx, Type leftType, Type rightType)
		{
			return null;
		}

		/// <summary>
		/// Emits operator code, in case it's not an overloaded method call.
		/// </summary>
		protected abstract void emitOperator(Context ctx);

		/// <summary>
		/// Calculates the constant value in compile time.
		/// </summary>
		protected abstract dynamic unrollConstant(dynamic left, dynamic right);

		#region Helper methods

		/// <summary>
		/// Loads both arguments and converts them to the biggest common type.
		/// </summary>
		protected void loadAndConvertNumerics(Context ctx, Type type = null)
		{
			var left = LeftOperand.Resolve(ctx);
			var right = RightOperand.Resolve(ctx);

			if(type == null)
				type = TypeExtensions.GetNumericOperationType(left, right);

			Expr.Cast(LeftOperand, type).Emit(ctx, true);
			Expr.Cast(RightOperand, type).Emit(ctx, true);
		}

		#endregion

		#region NodeBase overrides

		public override bool IsConstant { get { return RightOperand.IsConstant && LeftOperand.IsConstant; } }
		public override object ConstantValue { get { return unrollConstant(LeftOperand.ConstantValue, RightOperand.ConstantValue); } }

		protected override IEnumerable<NodeChild> getChildren()
		{
			yield return new NodeChild(LeftOperand, x => LeftOperand = x);
			yield return new NodeChild(RightOperand, x => RightOperand = x);
		}

		#endregion

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
