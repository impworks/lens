using Lens.SyntaxTree.Compiler;

namespace Lens.SyntaxTree.SyntaxTree.Literals
{
	/// <summary>
	/// A node that represents the `this` pointer.
	/// For compiler's internal usage only!
	/// </summary>
	public class ThisNode : NodeBase
	{
		protected override System.Type resolveExpressionType(Context ctx, bool mustReturn = true)
		{
			if(ctx.CurrentMethod.IsStatic)
				Error("Cannot access self-reference in static context!");

			return ctx.CurrentType.TypeBuilder;
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			GetExpressionType(ctx);

			var gen = ctx.CurrentILGenerator;
			gen.EmitLoadArgument(0);
		}
	}
}
