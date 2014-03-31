using Lens.Compiler;

namespace Lens.SyntaxTree.Internals
{
	/// <summary>
	/// A node that represents the `this` pointer.
	/// </summary>
	internal class ThisNode : NodeBase
	{
		protected override System.Type resolve(Context ctx, bool mustReturn)
		{
			if(ctx.CurrentMethod.IsStatic)
				error("Cannot access self-reference in static context!");

			return ctx.CurrentType.TypeBuilder;
		}

		protected override void emitCode(Context ctx, bool mustReturn)
		{
			Resolve(ctx);

			var gen = ctx.CurrentMethod.Generator;
			gen.EmitLoadArgument(0);
		}
	}
}
