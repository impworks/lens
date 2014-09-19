using System;
using Lens.Compiler;

namespace Lens.SyntaxTree.Internals
{
	/// <summary>
	/// A node that represents the `this` pointer.
	/// </summary>
	internal class ThisNode : NodeBase
	{
		#region Resolve

		protected override Type resolve(Context ctx, bool mustReturn)
		{
			if(ctx.CurrentMethod.IsStatic)
				error("Cannot access self-reference in static context!");

			return ctx.CurrentType.TypeBuilder;
		}

		#endregion

		#region Emit

		protected override void emitCode(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentMethod.Generator;
			gen.EmitLoadArgument(0);
		}

		#endregion
	}
}
