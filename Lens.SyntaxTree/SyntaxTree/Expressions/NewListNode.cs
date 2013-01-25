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
		protected override Type resolveExpressionType(Context ctx)
		{
			if(Expressions.Count == 0)
				Error("List must contain at least one object!");

			return typeof(List<>).MakeGenericType(Expressions[0].GetExpressionType(ctx));
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
