using System;
using Lens.SyntaxTree.Compiler;

namespace Lens.SyntaxTree.SyntaxTree.Operators
{
	/// <summary>
	/// A node representing the typeof operator.
	/// </summary>
	public class TypeofOperatorNode : TypeOperatorNodeBase
	{
		public TypeofOperatorNode(string type = null)
		{
			Type = type;
		}

		protected override Type resolveExpressionType(Context ctx)
		{
			return typeof (Type);
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentILGenerator;
			gen.EmitConstant(ctx.ResolveType(Type));
		}

		public override string ToString()
		{
			return string.Format("typeof({0})", Type);
		}
	}
}
