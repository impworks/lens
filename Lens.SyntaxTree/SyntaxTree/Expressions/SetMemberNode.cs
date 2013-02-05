using System;
using System.Collections.Generic;
using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.Expressions
{
	/// <summary>
	/// A node representing write access to a member of a type, field or property.
	/// </summary>
	public class SetMemberNode : MemberNodeBase
	{
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
			yield return Value;
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			throw new NotImplementedException();
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
