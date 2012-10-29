using System;
using System.Collections.Generic;

namespace Lens.SyntaxTree.SyntaxTree.Expressions
{
	/// <summary>
	/// A node representing a new List declaration.
	/// </summary>
	public class NewListNode : ValueListNodeBase<NodeBase>
	{
		public override Type GetExpressionType()
		{
			if (m_ExpressionType != null)
				return m_ExpressionType;

			if(Expressions.Count == 0)
				Error("List must contain at least one object!");

			m_ExpressionType = typeof(List<>).MakeGenericType(Expressions[0].GetExpressionType());
			return m_ExpressionType;
		}

		public override void Compile()
		{
			throw new NotImplementedException();
		}
	}
}
