using System;
using System.Collections.Generic;
using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.Operators
{
	/// <summary>
	/// A node representing a cast expression.
	/// </summary>
	public class CastOperatorNode : NodeBase
	{
		/// <summary>
		/// The expression to cast.
		/// </summary>
		public NodeBase Expression { get; set; }

		/// <summary>
		/// The type signature to cast to.
		/// </summary>
		public TypeSignature TypeSignature { get; set; }

		/// <summary>
		/// A resolved type to cast to.
		/// </summary>
		public Type Type { get; set; }

		public override IEnumerable<NodeBase> GetChildNodes()
		{
			yield return Expression;
		}
		
		protected override Type resolveExpressionType(Context ctx)
		{
			return Type ?? ctx.ResolveType(TypeSignature);
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentILGenerator;

			var fromType = Expression.GetExpressionType(ctx);
			var toType = Type ?? ctx.ResolveType(TypeSignature);

			if(!toType.IsExtendablyAssignableFrom(fromType) && !fromType.IsExtendablyAssignableFrom(toType))
				Error("Cannot cast object of type '{0}' to type '{1}'!");

			Expression.Compile(ctx, true);

			if (fromType == toType)
				return;

			if (fromType.IsValueType && toType == typeof(object))
				gen.EmitBox(fromType);

			else if (fromType == typeof(object) && toType.IsValueType)
				gen.EmitUnbox(toType);

			else if (toType.IsNullable() && Nullable.GetUnderlyingType(toType) == fromType)
			{
				var ctor = toType.GetConstructor(new[] { fromType });
				gen.EmitCreateObject(ctor);
			}

			else if (fromType.IsNumericType() && toType.IsNumericType())
				gen.EmitConvert(toType);

			else
				gen.EmitCast(toType, false);
		}

		#region Equality members

		protected bool Equals(CastOperatorNode other)
		{
			return Equals(Expression, other.Expression) && Equals(TypeSignature, other.TypeSignature);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((CastOperatorNode)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return ((Expression != null ? Expression.GetHashCode() : 0) * 397) ^ (TypeSignature != null ? TypeSignature.GetHashCode() : 0);
			}
		}

		#endregion
	}
}
