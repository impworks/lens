using System;
using System.Collections.Generic;
using Lens.Compiler;
using Lens.Utils;

namespace Lens.SyntaxTree.Operators.TypeBased
{
	/// <summary>
	/// The base node for operators that accept a type name.
	/// </summary>
	internal abstract class TypeOperatorNodeBase : NodeBase
	{
		#region Fields

		/// <summary>
		/// Type signature parameter.
		/// </summary>
		public TypeSignature TypeSignature { get; set; }

		/// <summary>
		/// Defined type parameter.
		/// </summary>
		public Type Type { get; set; }

		#endregion

		#region Debug

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
	internal abstract class TypeCheckOperatorNodeBase : TypeOperatorNodeBase
	{
		#region Fields

		/// <summary>
		///  The expression to check against the type.
		/// </summary>
		public NodeBase Expression { get; set; }

		#endregion

		#region Transform

		protected override IEnumerable<NodeChild> getChildren()
		{
			yield return new NodeChild(Expression, x => Expression = x);
		}

		#endregion

		#region Debug

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
