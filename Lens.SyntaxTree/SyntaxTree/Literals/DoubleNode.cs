using Lens.SyntaxTree.Compiler;

namespace Lens.SyntaxTree.SyntaxTree.Literals
{
	/// <summary>
	/// A node representing floating point double-precision literals.
	/// </summary>
	public class DoubleNode : LiteralNodeBase<double>
	{
		public DoubleNode(double value = 0)
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
