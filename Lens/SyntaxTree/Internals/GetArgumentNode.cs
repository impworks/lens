using System;
using Lens.Compiler;

namespace Lens.SyntaxTree.Internals
{
	/// <summary>
	/// Returns a local argument.
	/// </summary>
	[Obsolete]
	internal class GetArgumentNode : NodeBase
	{
		public int ArgumentId;

		protected override Type resolve(Context ctx, bool mustReturn)
		{
			return ctx.CurrentMethod.GetArgumentTypes(ctx)[ArgumentId];
		}

		protected override void emitCode(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentMethod.Generator;

			var id = ArgumentId + (ctx.CurrentMethod.IsStatic ? 0 : 1);
			gen.EmitLoadArgument(id);
		}
	}
}
