using Lens.Compiler;

namespace Lens.SyntaxTree.Literals
{
	/// <summary>
	/// A node representing "true" or "false" literals.
	/// </summary>
	internal class BooleanNode : LiteralNodeBase<bool>
	{
		#region Constructor

		public BooleanNode(bool value = false)
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
