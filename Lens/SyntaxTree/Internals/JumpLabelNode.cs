using System.Reflection.Emit;

using Lens.Compiler;

namespace Lens.SyntaxTree.Internals
{
	/// <summary>
	/// Represents the destination for an unconditional jump-to-label (GOTO).
	/// </summary>
	internal class JumpLabelNode : NodeBase, IMetaNode
	{
		#region Constructor

		public JumpLabelNode(Label label)
		{
			Label = label;
		}

		#endregion

		#region Fields

		/// <summary>
		/// The label to place at current location.
		/// </summary>
		private readonly Label Label;

		#endregion

		#region Emit

		protected override void emitCode(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentMethod.Generator;

			gen.MarkLabel(Label);
		}

		#endregion
	}
}
