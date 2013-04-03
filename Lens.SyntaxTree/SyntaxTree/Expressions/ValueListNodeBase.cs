using System;
using System.Collections.Generic;
using System.Linq;
using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.SyntaxTree.Literals;
using Lens.SyntaxTree.Translations;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.Expressions
{
	/// <summary>
	/// Base node for value lists: dictionaries, arrays, lists etc.
	/// </summary>
	public abstract class ValueListNodeBase<T> : NodeBase, IStartLocationTrackingEntity, IEndLocationTrackingEntity
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
				if(curr.GetExpressionType(ctx).IsVoid())
					Error(curr, CompilerMessages.ExpressionVoid);

			var types = nodes.Select(n => n.GetExpressionType(ctx)).Select(t => t == typeof (NullType) ? typeof (object) : t).ToArray();
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
