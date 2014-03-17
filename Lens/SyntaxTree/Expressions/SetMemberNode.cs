using System.Collections.Generic;
using Lens.Compiler;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.Expressions
{
	/// <summary>
	/// A node representing write access to a member of a type, field or property.
	/// </summary>
	internal class SetMemberNode : MemberNodeBase
	{
		private bool m_IsStatic;
		private PropertyWrapper m_Property;
		private FieldWrapper m_Field;

		/// <summary>
		/// Value to be assigned.
		/// </summary>
		public NodeBase Value { get; set; }

		public override IEnumerable<NodeChild> GetChildren()
		{
			yield return new NodeChild(Expression, x => Expression = x);
			yield return new NodeChild(Value, x => Value = x);
		}

		protected override void emitCode(Context ctx, bool mustReturn)
		{
			resolve(ctx);

			var gen = ctx.CurrentILGenerator;

			var destType = m_Field != null ? m_Field.FieldType : m_Property.PropertyType;
			var valType = Value.Resolve(ctx);

			ctx.CheckTypedExpression(Value, valType, true);

			if(!destType.IsExtendablyAssignableFrom(valType))
				error(CompilerMessages.ImplicitCastImpossible, valType, destType);

			if (!m_IsStatic)
			{
				var exprType = Expression.Resolve(ctx);
				if (Expression is IPointerProvider && exprType.IsStruct())
					(Expression as IPointerProvider).PointerRequired = true;

				Expression.Emit(ctx, true);
			}

			Expr.Cast(Value, destType).Emit(ctx, true);

			if(m_Field != null)
				gen.EmitSaveField(m_Field.FieldInfo);
			else
				gen.EmitCall(m_Property.Setter, true);
		}

		private void resolve(Context ctx)
		{
			var type = StaticType != null
				? ctx.ResolveType(StaticType)
				: Expression.Resolve(ctx);

			checkTypeInSafeMode(ctx, type);

			// check for field
			try
			{
				m_Field = ctx.ResolveField(type, MemberName);
				m_IsStatic = m_Field.IsStatic;
				if (Expression == null && !m_IsStatic)
					error(CompilerMessages.DynamicMemberFromStaticContext, type, MemberName);

				return;
			}
			catch (KeyNotFoundException) { }

			try
			{
				m_Property = ctx.ResolveProperty(type, MemberName);
				if(!m_Property.CanSet)
					error(CompilerMessages.PropertyNoSetter, MemberName, type);

				m_IsStatic = m_Property.IsStatic;
				if (Expression == null && !m_IsStatic)
					error(CompilerMessages.DynamicMemberFromStaticContext, MemberName, type);
			}
			catch (KeyNotFoundException)
			{
				error(CompilerMessages.TypeSettableIdentifierNotFound, type, MemberName);
			}
		}

		#region Equality members

		protected bool Equals(SetMemberNode other)
		{
			return base.Equals(other) && Equals(Value, other.Value);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((SetMemberNode)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return (base.GetHashCode() * 397) ^ (Value != null ? Value.GetHashCode() : 0);
			}
		}

		#endregion

		public override string ToString()
		{
			return string.Format("setmbr({0} of {1} = {2})", MemberName, Expression, Value);
		}
	}
}
