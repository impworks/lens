using System.Collections.Generic;
using System.Reflection;
using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.Expressions
{
	/// <summary>
	/// A node representing write access to a member of a type, field or property.
	/// </summary>
	public class SetMemberNode : MemberNodeBase
	{
		private bool m_IsStatic;
		private PropertyInfo m_Property;
		private FieldInfo m_Field;

		/// <summary>
		/// Value to be assigned.
		/// </summary>
		public NodeBase Value { get; set; }

		public override LexemLocation EndLocation
		{
			get { return Value.EndLocation; }
			set { LocationSetError(); }
		}

		public override IEnumerable<NodeBase> GetChildNodes()
		{
			if (Expression != null)
				yield return Expression;
			yield return Value;
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			resolve(ctx);

			var gen = ctx.CurrentILGenerator;

			var destType = m_Field != null ? m_Field.FieldType : m_Property.PropertyType;
			var valType = Value.GetExpressionType(ctx);

			if (valType.IsVoid())
				Error("Cannot use a function or expression that does not return anything as assignment source!");

			// todo: extract, add check for null
			if(!destType.IsExtendablyAssignableFrom(valType))
				Error("Cannot implicitly convert an object of type '{0}' to required type '{1}'!", valType, destType);

			if (!m_IsStatic)
			{
				var exprType = Expression.GetExpressionType(ctx);
				if (Expression is IPointerProvider && exprType.IsStruct())
					(Expression as IPointerProvider).PointerRequired = true;

				Expression.Compile(ctx, true);
			}

			Expr.Cast(Value, destType).Compile(ctx, true);

			if(m_Field != null)
				gen.EmitSaveField(m_Field);
			else
				gen.EmitCall(m_Property.GetSetMethod(), true);
		}

		private void resolve(Context ctx)
		{
			var type = StaticType != null
				? ctx.ResolveType(StaticType)
				: Expression.GetExpressionType(ctx);

			// check for field
			try
			{
				m_Field = ctx.ResolveField(type, MemberName);
				m_IsStatic = m_Field.IsStatic;
				if (Expression == null && !m_IsStatic)
					Error("Field '{1}' of type '{0}' cannot be used in static context!", type, MemberName);

				return;
			}
			catch (KeyNotFoundException) { }

			try
			{
				m_Property = ctx.ResolveProperty(type, MemberName);
				var setMbr = m_Property.GetSetMethod();
				if(setMbr == null)
					Error("Property '{0}' of type '{1}' does not have a public setter!");

				m_IsStatic = setMbr.IsStatic;
				if (Expression == null && !m_IsStatic)
					Error("Property '{0}' of type '{1}' cannot be used in static context!", type, MemberName);
			}
			catch (KeyNotFoundException)
			{
				Error("Type '{0}' does not contain a field or a property named '{1}'!", type, MemberName);
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
