using Lens.SyntaxTree.Compiler;

namespace Lens.SyntaxTree.SyntaxTree.Literals
{
	/// <summary>
	/// A node representing an integer literal.
	/// </summary>
	public class IntNode : LiteralNodeBase<int>
	{
		public IntNode(int value = 0)
		{
			Value = value;
		}

		protected override void compile(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentILGenerator;
			gen.EmitConstant(Value);
		}
	}
}
