using Lens.Compiler;

namespace Lens.SyntaxTree.Literals
{
	/// <summary>
	/// A node representing a single unicode character.
	/// </summary>
	internal class CharNode : LiteralNodeBase<char>
	{
		public CharNode(char value)
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
