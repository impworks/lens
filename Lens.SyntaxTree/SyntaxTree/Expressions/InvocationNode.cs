using System;
using System.Collections.Generic;
using Lens.SyntaxTree.Compiler;

namespace Lens.SyntaxTree.SyntaxTree.Expressions
{
	/// <summary>
	/// A node representing a method being invoked.
	/// </summary>
	public class InvocationNode : InvocationNodeBase
	{
		/// <summary>
		/// An expression to invoke the method on.
		/// </summary>
		public NodeBase Expression { get; set; }

		protected override Type resolveExpressionType(Context ctx, bool mustReturn = true)
		{
			throw new NotImplementedException();
		}

		public override IEnumerable<NodeBase> GetChildNodes()
		{
			if (Expression != null)
				yield return Expression;

			foreach (var curr in Arguments)
				yield return curr;
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			throw new NotImplementedException();
		}

		#region Equality members

		protected bool Equals(InvocationNode other)
		{
			return base.Equals(other)
				&& Equals(Expression, other.Expression);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((InvocationNode)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				int hashCode = base.GetHashCode();
				hashCode = (hashCode * 397) ^ (Expression != null ? Expression.GetHashCode() : 0);
				return hashCode;
			}
		}

		#endregion

		public override string ToString()
		{
			return string.Format("invoke({0}, args: ({1}))", Expression, string.Join(",", Arguments));
		}
	}
}
