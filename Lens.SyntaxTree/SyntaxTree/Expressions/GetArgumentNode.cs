using System;
using Lens.SyntaxTree.Compiler;

namespace Lens.SyntaxTree.SyntaxTree.Expressions
{
	public class GetArgumentNode : NodeBase
	{
		public int ArgumentId;

		protected override Type resolveExpressionType(Context ctx, bool mustReturn = true)
		{
			return ctx.CurrentMethod.GetArgumentTypes(ctx)[ArgumentId];
		}

		protected override void compile(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentILGenerator;

			var id = ArgumentId + (ctx.CurrentMethod.IsStatic ? 0 : 1);
			gen.EmitLoadArgument(id);
		}
	}
}
