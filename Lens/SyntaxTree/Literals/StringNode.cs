using Lens.Compiler;

namespace Lens.SyntaxTree.Literals
{
	/// <summary>
	/// A node representing string literals.
	/// </summary>
	internal class StringNode : LiteralNodeBase<string>
	{
		public StringNode(string value = null)
		{
			Value = value;
		}

		protected override void emitCode(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentILGenerator;
			gen.EmitConstant(Value);
		}
	}
}
