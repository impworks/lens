using System;
using System.Collections.Generic;

namespace Lens.SyntaxTree.SyntaxTree.Literals
{
	/// <summary>
	/// The base class for literal nodes.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public abstract class LiteralNodeBase<T> : NodeBase
	{
		/// <summary>
		/// The literal value.
		/// </summary>
		public T Value { get; set; }

		public override Type GetExpressionType()
		{
			return typeof (T);
		}

		#region Equality members

		protected bool Equals(LiteralNodeBase<T> other)
		{
			return EqualityComparer<T>.Default.Equals(Value, other.Value);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((LiteralNodeBase<T>)obj);
		}

		public override int GetHashCode()
		{
			return EqualityComparer<T>.Default.GetHashCode(Value);
		}

		#endregion
	}
}
