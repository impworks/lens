using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Lens.SyntaxTree.SyntaxTree.Expressions
{
	/// <summary>
	/// A node representing a new dictionary.
	/// </summary>
	public class NewDictionaryNode : NodeBase
	{
		public NewDictionaryNode()
		{
			Expressions = new List<KeyValuePair<NodeBase, NodeBase>>();
		}

		/// <summary>
		/// The list of items in the array.
		/// </summary>
		public List<KeyValuePair<NodeBase, NodeBase>> Expressions { get; set; }

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
	}
}
