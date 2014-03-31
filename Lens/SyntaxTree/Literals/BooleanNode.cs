using Lens.Compiler;

namespace Lens.SyntaxTree.Literals
{
	/// <summary>
	/// A node representing "true" or "false" literals.
	/// </summary>
	internal class BooleanNode : LiteralNodeBase<bool>
	{
		public BooleanNode(bool value = false)
		{
			Value = value;
		}

		protected override void emitCode(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentMethod.Generator;
			gen.EmitConstant(Value);
		}
	}
}
