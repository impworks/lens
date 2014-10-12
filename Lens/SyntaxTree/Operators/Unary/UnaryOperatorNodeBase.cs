using System;
using System.Collections.Generic;
using Lens.Compiler;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.Operators.Unary
{
	/// <summary>
	/// The base for all unary operators.
	/// </summary>
	internal abstract class UnaryOperatorNodeBase : OperatorNodeBase
	{
		#region Fields

		/// <summary>
		/// The operand.
		/// </summary>
		public NodeBase Operand { get; set; }

		#endregion

		#region Resolve

		protected override Type resolve(Context ctx, bool mustReturn)
		{
			var type = Operand.Resolve(ctx);

			var result = resolveOperatorType(ctx);
			if (result != null)
				return result;

			if (OverloadedMethodName != null)
			{
				try
				{
					_OverloadedMethod = ctx.ResolveMethod(type, OverloadedMethodName, new[] { type });

					// cannot be generic
					if (_OverloadedMethod != null)
						return _OverloadedMethod.ReturnType;
				}
				catch { }
			}

			error(CompilerMessages.OperatorUnaryTypeMismatch, OperatorRepresentation, type);
			return null;
		}

		/// <summary>
		/// Overridable resolver for unary operators.
		/// </summary>
		protected virtual Type resolveOperatorType(Context ctx)
		{
			return null;
		}

		#endregion

		#region Transform

		protected override IEnumerable<NodeChild> getChildren()
		{
			yield return new NodeChild(Operand, x => Operand = x);
		}

		#endregion

		#region Emit

		protected override void emitCode(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentMethod.Generator;

			if (_OverloadedMethod == null)
			{
				emitOperator(ctx);
				return;
			}

			var ps = _OverloadedMethod.ArgumentTypes;
			Expr.Cast(Operand, ps[0]).Emit(ctx, true);
			gen.EmitCall(_OverloadedMethod.MethodInfo);
		}

		protected abstract void emitOperator(Context ctx);

		#endregion

		#region Constant unroll

		public override bool IsConstant { get { return Operand.IsConstant; } }
		public override dynamic ConstantValue { get { return unrollConstant(Operand.ConstantValue); } }

		/// <summary>
		/// Overriddable constant unroller for unary operators.
		/// </summary>
		protected abstract dynamic unrollConstant(dynamic value);

		#endregion

		#region Debug

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

		public override string ToString()
		{
			return string.Format("op{0}({1})", OperatorRepresentation, Operand);
		}

		#endregion
	}
}
