using System;
using Lens.Compiler;

namespace Lens.SyntaxTree.Literals
{
	/// <summary>
	/// A node representing a decimal floating point literal.
	/// </summary>
	internal class DecimalNode : LiteralNodeBase<decimal>
	{
		public DecimalNode(decimal value = 0)
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
