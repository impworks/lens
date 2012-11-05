using System;
using System.Collections.Generic;
using System.Linq;

namespace Lens.SyntaxTree.SyntaxTree.Expressions
{
	/// <summary>
	/// A node representing a new dictionary.
	/// </summary>
	public class NewDictionaryNode : ValueListNodeBase<KeyValuePair<NodeBase, NodeBase>>
	{
		#region Equality members

		protected bool Equals(NewDictionaryNode other)
		{
			// KeyValuePair doesn't have Equals overridden, that's why it's so messy here:
			return Expressions.Select(e => e.Key).SequenceEqual(other.Expressions.Select(e => e.Key))
			       && Expressions.Select(e => e.Value).SequenceEqual(other.Expressions.Select(e => e.Value));
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((NewDictionaryNode) obj);
		}

		#endregion

		public override Type GetExpressionType()
		{
			if (m_ExpressionType != null)
				return m_ExpressionType;

			if(Expressions.Count == 0)
				Error("List must contain at least one object!");

			m_ExpressionType = typeof(Dictionary<,>).MakeGenericType(
				Expressions[0].Key.GetExpressionType(),
				Expressions[0].Value.GetExpressionType()
			);

			return m_ExpressionType;
		}

		public override void Compile()
		{
			throw new NotImplementedException();
		}

		public override string ToString()
		{
			return string.Format("dict({0})", string.Join(";", Expressions.Select(x => string.Format("{0} => {1}", x.Key, x.Value))));
		}
	}
}
