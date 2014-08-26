using System;
using System.Collections.Generic;
using Lens.Compiler;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.Operators
{
	/// <summary>
	/// The base for all unary operators.
	/// </summary>
	internal abstract class UnaryOperatorNodeBase : OperatorNodeBase
	{
		/// <summary>
		/// The operand.
		/// </summary>
		public NodeBase Operand { get; set; }

		public override bool IsConstant { get { return Operand.IsConstant; } }
		public override dynamic ConstantValue { get { return unrollConstant(Operand.ConstantValue); } }

		protected override IEnumerable<NodeChild> getChildren()
		{
			yield return new NodeChild(Operand, x => Operand = x);
		}

		protected override Type resolve(Context ctx, bool mustReturn = true)
		{
			var type = Operand.Resolve(ctx);

			var result = resolveOperatorType(ctx);
			if (result != null)
				return result;

			if (OverloadedMethodName != null)
			{
				try
				{
					m_OverloadedMethod = ctx.ResolveMethod(type, OverloadedMethodName, new[] { type });

					// cannot be generic
					if (m_OverloadedMethod != null)
						return m_OverloadedMethod.ReturnType;
				}
				catch { }
			}

			error(CompilerMessages.OperatorUnaryTypeMismatch, OperatorRepresentation, type);
			return null;
		}

		protected override void emitCode(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentMethod.Generator;

			if (m_OverloadedMethod == null)
			{
				compileOperator(ctx);
				return;
			}

			var ps = m_OverloadedMethod.ArgumentTypes;
			Expr.Cast(Operand, ps[0]).Emit(ctx, true);
			gen.EmitCall(m_OverloadedMethod.MethodInfo);
		}

		protected virtual Type resolveOperatorType(Context ctx)
		{
			return null;
		}

		protected abstract void compileOperator(Context ctx);
		protected abstract dynamic unrollConstant(dynamic value);

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

		public override string ToString()
		{
			return string.Format("op{0}({1})", OperatorRepresentation, Operand);
		}
	}
}
