using Lens.SyntaxTree.Compiler;

namespace Lens.SyntaxTree.SyntaxTree.Literals
{
	/// <summary>
	/// A node representing string literals.
	/// </summary>
	public class StringNode : LiteralNodeBase<string>
	{
		public StringNode(string value = null)
		{
			Value = value;
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentILGenerator;
			gen.EmitConstant(Value);
		}
	}
}
