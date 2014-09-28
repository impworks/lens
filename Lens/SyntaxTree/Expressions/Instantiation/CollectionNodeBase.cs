using System;
using System.Collections.Generic;
using System.Linq;
using Lens.Compiler;
using Lens.Resolver;
using Lens.Translations;

namespace Lens.SyntaxTree.Expressions.Instantiation
{
	/// <summary>
	/// Base node for collections: dictionaries, arrays, lists, etc.
	/// </summary>
	internal abstract class CollectionNodeBase<T> : NodeBase
	{
		#region Constructor

		protected CollectionNodeBase()
		{
			Expressions = new List<T>();
		}

		#endregion

		#region Fields

		/// <summary>
		/// The list of items.
		/// </summary>
		public List<T> Expressions { get; set; }

		#endregion

		#region Resolve

		protected Type resolveItemType(IEnumerable<NodeBase> nodes, Context ctx)
		{
			foreach(var curr in nodes)
				if(curr.Resolve(ctx).IsVoid())
					error(curr, CompilerMessages.ExpressionVoid);

			var types = nodes.Select(n => n.Resolve(ctx)).ToArray();
			return types.GetMostCommonType();
		}

		#endregion

		#region Debug

		protected bool Equals(CollectionNodeBase<T> other)
		{
			return Expressions.SequenceEqual(other.Expressions);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((CollectionNodeBase<T>)obj);
		}

		public override int GetHashCode()
		{
			return (Expressions != null ? Expressions.GetHashCode() : 0);
		}

		#endregion
	}
}
