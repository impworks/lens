using System.Reflection.Emit;
using Lens.Compiler;

namespace Lens.SyntaxTree.Internals
{
	/// <summary>
	/// Represents an unconditional jump-to-label node (GOTO).
	/// </summary>
	internal class JumpNode : NodeBase
	{
		#region Constructor

		public JumpNode(Label label)
		{
			DestinationLabel = label;
		}

		#endregion

		#region Fields

		/// <summary>
		/// The label to jump to.
		/// </summary>
		private readonly Label DestinationLabel;

		#endregion

		#region Emit

		protected override void emitCode(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentMethod.Generator;

			gen.EmitJump(DestinationLabel);
		}

		#endregion
	}
}
