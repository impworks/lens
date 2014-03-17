using Lens.Compiler;

namespace Lens.SyntaxTree.Literals
{
	/// <summary>
	/// A node that represents the `this` pointer.
	/// For compiler's internal usage only!
	/// </summary>
	internal class ThisNode : NodeBase
	{
		protected override System.Type resolve(Context ctx, bool mustReturn = true)
		{
			if(ctx.CurrentMethod.IsStatic)
				error("Cannot access self-reference in static context!");

			return ctx.CurrentType.TypeBuilder;
		}

		protected override void emitCode(Context ctx, bool mustReturn)
		{
			Resolve(ctx);

			var gen = ctx.CurrentILGenerator;
			gen.EmitLoadArgument(0);
		}
	}
}
