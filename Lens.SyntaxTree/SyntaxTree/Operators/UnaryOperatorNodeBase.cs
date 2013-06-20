using System;
using System.Collections.Generic;
using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.Translations;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.Operators
{
	/// <summary>
	/// The base for all unary operators.
	/// </summary>
	public abstract class UnaryOperatorNodeBase : OperatorNodeBase, IStartLocationTrackingEntity
	{
		/// <summary>
		/// The operand.
		/// </summary>
		public NodeBase Operand { get; set; }

		public override LexemLocation EndLocation
		{
			get { return Operand.EndLocation; }
			set { LocationSetError(); }
		}

		public override IEnumerable<NodeBase> GetChildNodes()
		{
			yield return Operand;
		}

		protected override Type resolveExpressionType(Context ctx, bool mustReturn = true)
		{
			var type = Operand.GetExpressionType(ctx);

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

			Error(CompilerMessages.OperatorUnaryTypeMismatch, OperatorRepresentation, type);
			return null;
		}

		protected override void compile(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentILGenerator;

			GetExpressionType(ctx);

			if (m_OverloadedMethod == null)
			{
				compileOperator(ctx);
				return;
			}

			var ps = m_OverloadedMethod.ArgumentTypes;
			Expr.Cast(Operand, ps[0]).Compile(ctx, true);
			gen.EmitCall(m_OverloadedMethod.MethodInfo);
		}

		protected virtual Type resolveOperatorType(Context ctx)
		{
			return null;
		}

		protected abstract void compileOperator(Context ctx);

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
