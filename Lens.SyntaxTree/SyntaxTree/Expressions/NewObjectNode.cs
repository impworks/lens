using System;
using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.Expressions
{
	/// <summary>
	/// A node representing a new object creation.
	/// </summary>
	public class NewObjectNode : InvocationNodeBase
	{
		public NewObjectNode(string type = null)
		{
			Type = type;
		}

		/// <summary>
		/// The type of the object to create.
		/// </summary>
		public TypeSignature Type { get; set; }

		public override Type GetExpressionType(Context ctx)
		{
			return Type.Type;
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			throw new NotImplementedException();
		}

		#region Equality members

		protected bool Equals(NewObjectNode other)
		{
			return base.Equals(other) && Equals(Type, other.Type);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((NewObjectNode)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return (base.GetHashCode() * 397) ^ (Type != null ? Type.GetHashCode() : 0);
			}
		}

		#endregion

		public override string ToString()
		{
			return string.Format("new({0}, args: {1})", Type, string.Join(";", Arguments));
		}
	}
}
