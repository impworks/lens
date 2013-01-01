using System;
using System.Collections.Generic;
using Lens.SyntaxTree.Compiler;

namespace Lens.SyntaxTree.SyntaxTree.Expressions
{
	/// <summary>
	/// A node representing a new List declaration.
	/// </summary>
	public class NewListNode : ValueListNodeBase<NodeBase>
	{
		public override Type GetExpressionType(Context ctx)
		{
			if (m_ExpressionType != null)
				return m_ExpressionType;

			if(Expressions.Count == 0)
				Error("List must contain at least one object!");

			m_ExpressionType = typeof(List<>).MakeGenericType(Expressions[0].GetExpressionType(ctx));
			return m_ExpressionType;
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			throw new NotImplementedException();
		}

		public override string ToString()
		{
			return string.Format("list({0})", string.Join(";", Expressions));
		}
	}
}
