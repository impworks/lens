using System;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.Operators
{
	/// <summary>
	/// A node representing the typeof operator.
	/// </summary>
	public class TypeofOperatorNode : NodeBase
	{
		/// <summary>
		/// Type to be inspected.
		/// </summary>
		public TypeSignature Type { get; set; }

		public override Type GetExpressionType()
		{
			return typeof (Type);
		}

		public override void Compile()
		{
			throw new NotImplementedException();
		}

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
