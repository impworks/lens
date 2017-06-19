using Lens.Compiler;

namespace Lens.SyntaxTree.Literals
{
	/// <summary>
	/// A node representing a decimal floating point literal.
	/// </summary>
	internal class DecimalNode : LiteralNodeBase<decimal>
	{
		#region Constructor

		public DecimalNode(decimal value = 0)
		{
			Value = value;
		}

		#endregion

		#region Emit

		protected override void EmitCode(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentMethod.Generator;

			gen.EmitConstant(Value);
		}

		#endregion
	}
}
