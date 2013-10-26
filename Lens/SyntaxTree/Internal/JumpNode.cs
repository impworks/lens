using System.Reflection.Emit;
using Lens.Compiler;

namespace Lens.SyntaxTree.SyntaxTree.Internal
{
	/// <summary>
	/// A node that describes an unconditional jump to a label.
	/// </summary>
	internal class JumpNode : NodeBase
	{
		public JumpNode(Label label)
		{
			m_Label = label;
		}

		private readonly Label m_Label;

		protected override void compile(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentILGenerator;
			gen.EmitJump(m_Label);
		}
	}
}
