using Lens.Compiler;

namespace Lens.SyntaxTree.Expressions.GetSet
{
	/// <summary>
	/// The base node for member getters and setters.
	/// </summary>
	abstract internal class MemberNodeBase : AccessorNodeBase
	{
		#region Fields

		/// <summary>
		/// Type signature to access a static type.
		/// </summary>
		public TypeSignature StaticType { get; set; }

		/// <summary>
		/// The name of the member to access.
		/// </summary>
		public string MemberName { get; set; }

		#endregion

		#region Debug

		protected bool Equals(MemberNodeBase other)
		{
			return Equals(Expression, other.Expression)
				&& string.Equals(MemberName, other.MemberName)
				&& Equals(StaticType, other.StaticType);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((MemberNodeBase)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				int hashCode = (Expression != null ? Expression.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ (MemberName != null ? MemberName.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ (StaticType != null ? StaticType.GetHashCode() : 0);
				return hashCode;
			}
		}

		#endregion
	}
}
