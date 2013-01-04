using System;
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
		/// The type to cast to.
		/// </summary>
		public TypeSignature Type { get; set; }


		public override Type GetExpressionType(Context ctx)
		{
			return ctx.ResolveType(Type.Signature);
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			throw new NotImplementedException();
		}

		#region Equality members

		protected bool Equals(CastOperatorNode other)
		{
			return Equals(Expression, other.Expression) && Equals(Type, other.Type);
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
				return ((Expression != null ? Expression.GetHashCode() : 0) * 397) ^ (Type != null ? Type.GetHashCode() : 0);
			}
		}

		#endregion
	}
}
