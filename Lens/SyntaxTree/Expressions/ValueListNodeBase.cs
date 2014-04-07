using System;
using System.Collections.Generic;
using System.Linq;
using Lens.Compiler;
using Lens.Resolver;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.Expressions
{
	/// <summary>
	/// Base node for value lists: dictionaries, arrays, lists etc.
	/// </summary>
	internal abstract class ValueListNodeBase<T> : NodeBase
	{
		protected ValueListNodeBase()
		{
			Expressions = new List<T>();
		}

		/// <summary>
		/// The list of items.
		/// </summary>
		public List<T> Expressions { get; set; }

		protected Type resolveItemType(IEnumerable<NodeBase> nodes, Context ctx)
		{
			foreach(var curr in nodes)
				if(curr.Resolve(ctx).IsVoid())
					error(curr, CompilerMessages.ExpressionVoid);

			var types = nodes.Select(n => n.Resolve(ctx)).ToArray();
			return types.GetMostCommonType();
		}

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
