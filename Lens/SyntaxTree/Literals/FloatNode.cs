using Lens.Compiler;

namespace Lens.SyntaxTree.Literals
{
	/// <summary>
	/// A node representing a single-precision floating point number literal.
	/// </summary>
	internal class FloatNode : LiteralNodeBase<float>
	{
		public FloatNode(float value = 0)
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