using System;

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

		/// <summary>
		/// The name of the method to invoke.
		/// </summary>
		public string MethodName { get; set; }

		public override Type GetExpressionType()
		{
			throw new NotImplementedException();
		}

		public override void Compile()
		{
			throw new NotImplementedException();
		}

		#region Equality members

		protected bool Equals(InvocationNode other)
		{
			return base.Equals(other)
				&& Equals(Expression, other.Expression)
				&& string.Equals(MethodName, other.MethodName);
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
				hashCode = (hashCode * 397) ^ (MethodName != null ? MethodName.GetHashCode() : 0);
				return hashCode;
			}
		}

		#endregion

		public override string ToString()
		{
			return string.Format("invoke({0} of {1}, args: ({2}))", MethodName, Expression, string.Join(",", Arguments));
		}
	}
}
