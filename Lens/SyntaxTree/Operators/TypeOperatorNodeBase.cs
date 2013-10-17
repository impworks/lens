using System;
using System.Collections.Generic;
using Lens.Compiler;

namespace Lens.SyntaxTree.Operators
{
	/// <summary>
	/// The base node for operators that accept a type name.
	/// </summary>
	public abstract class TypeOperatorNodeBase : NodeBase
	{
		/// <summary>
		/// Type signature parameter.
		/// </summary>
		public TypeSignature TypeSignature { get; set; }

		/// <summary>
		/// Defined type parameter.
		/// </summary>
		public Type Type { get; set; }

		#region Equality

		protected bool Equals(TypeOperatorNodeBase other)
		{
			return Type == other.Type && Equals(TypeSignature, other.TypeSignature);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((TypeOperatorNodeBase)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return ((Type != null ? Type.GetHashCode() : 0) * 397) ^ (TypeSignature != null ? TypeSignature.GetHashCode() : 0);
			}
		}

		#endregion
	}

	/// <summary>
	/// The base node for operators that accept a type name.
	/// </summary>
	public abstract class TypeCheckOperatorNodeBase : TypeOperatorNodeBase
	{
		/// <summary>
		///  The expression to check against the type.
		/// </summary>
		public NodeBase Expression { get; set; }

		public override IEnumerable<NodeBase> GetChildNodes()
		{
			yield return Expression;
		}

		#region Equality

		protected bool Equals(TypeCheckOperatorNodeBase other)
		{
			return base.Equals(other) && Equals(Expression, other.Expression);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((TypeCheckOperatorNodeBase)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return (base.GetHashCode() * 397) ^ (Expression != null ? Expression.GetHashCode() : 0);
			}
		}

		#endregion
	}
}
