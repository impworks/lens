using System.Collections.Generic;
using System.Linq;

namespace Lens.SyntaxTree.SyntaxTree.Expressions
{
	/// <summary>
	/// Base node for value lists: dictionaries, arrays, lists etc.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public abstract class ValueListNodeBase<T> : NodeBase
	{
		protected ValueListNodeBase()
		{
			Expressions = new List<T>();
		}

		/// <summary>
		/// The list of items.
		/// </summary>
		public List<T> Expressions { get; set; }

		#region Equality members

		protected bool Equals(ValueListNodeBase<T> other)
		{
			return Expressions.SequenceEqual(other.Expressions);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((ValueListNodeBase<T>)obj);
		}

		public override int GetHashCode()
		{
			return (Expressions != null ? Expressions.GetHashCode() : 0);
		}

		#endregion
	}
}
