using Lens.Compiler;

namespace Lens.SyntaxTree.Literals
{
	/// <summary>
	/// A node representing a long literal.
	/// </summary>
	internal class LongNode : LiteralNodeBase<long>
	{
		public LongNode(long value = 0)
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
