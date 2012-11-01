using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.Operators
{
	/// <summary>
	/// The base node for operators that accept a type name.
	/// </summary>
	public abstract class TypeOperatorNodeBase : NodeBase
	{
		/// <summary>
		/// Type parameter.
		/// </summary>
		public TypeSignature Type { get; set; }

		#region Equality members

		protected bool Equals(TypeofOperatorNode other)
		{
			return Equals(Type, other.Type);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((TypeofOperatorNode)obj);
		}

		public override int GetHashCode()
		{
			return (Type != null ? Type.GetHashCode() : 0);
		}

		#endregion
	}
}
