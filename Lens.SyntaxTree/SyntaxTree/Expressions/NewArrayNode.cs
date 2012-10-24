using System;
using System.Collections.Generic;

namespace Lens.SyntaxTree.SyntaxTree.Expressions
{
	/// <summary>
	/// A node representing a new array declaration.
	/// </summary>
	public class NewArrayNode : NodeBase
	{
		public NewArrayNode()
		{
			Expressions = new List<NodeBase>();
		}

		/// <summary>
		/// The list of items in the array.
		/// </summary>
		public List<NodeBase> Expressions { get; set; }

		public override Type GetExpressionType()
		{
			if (m_ExpressionType != null)
				return m_ExpressionType;

			if(Expressions.Count == 0)
				Error("Array must contain at least one object!");

			m_ExpressionType = Expressions[0].GetExpressionType().MakeArrayType();
			return m_ExpressionType;
		}

		public override void Compile()
		{
			throw new NotImplementedException();
		}
	}
}
