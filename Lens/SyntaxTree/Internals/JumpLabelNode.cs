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
            _label = label;
        }

        #endregion

        #region Fields

        /// <summary>
        /// The label to place at current location.
        /// </summary>
        private readonly Label _label;

        #endregion

        #region Emit

        protected override void EmitCode(Context ctx, bool mustReturn)
        {
            var gen = ctx.CurrentMethod.Generator;

            gen.MarkLabel(_label);
        }

        #endregion
    }
}