using System;

namespace Lens.SyntaxTree.SyntaxTree.Expressions
{
	/// <summary>
	/// A node representing a new array declaration.
	/// </summary>
	public class NewArrayNode : ValueListNodeBase<NodeBase>
	{
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

		public override string ToString()
		{
			return string.Format("array({0})", string.Join(";", Expressions));
		}
	}
}
