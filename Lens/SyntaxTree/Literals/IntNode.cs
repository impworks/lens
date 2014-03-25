using Lens.Compiler;

namespace Lens.SyntaxTree.Literals
{
	/// <summary>
	/// A node representing an integer literal.
	/// </summary>
	internal class IntNode : LiteralNodeBase<int>
	{
		public IntNode(int value = 0)
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
