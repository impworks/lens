using System;
using System.Collections.Generic;
using Lens.SyntaxTree.Compiler;

namespace Lens.SyntaxTree.SyntaxTree.Literals
{
	/// <summary>
	/// The base class for literal nodes.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public abstract class LiteralNodeBase<T> : NodeBase, IStartLocationTrackingEntity, IEndLocationTrackingEntity
	{
		/// <summary>
		/// The literal value.
		/// </summary>
		public T Value { get; set; }

		protected override Type resolveExpressionType(Context ctx, bool mustReturn = true)
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

		public override string ToString()
		{
			return string.Format("{0}({1})", typeof (T).Name, Value);
		}
	}
}
