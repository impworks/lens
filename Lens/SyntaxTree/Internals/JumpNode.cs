using System.Reflection.Emit;
using Lens.Compiler;

namespace Lens.SyntaxTree.Internals
{
    /// <summary>
    /// Represents an unconditional jump-to-label node (GOTO).
    /// </summary>
    internal class JumpNode : NodeBase, IMetaNode
    {
        #region Constructor

        public JumpNode(Label label)
        {
            _destinationLabel = label;
        }

        #endregion

        #region Fields

        /// <summary>
        /// The label to jump to.
        /// </summary>
        private readonly Label _destinationLabel;

        #endregion

        #region Emit

        protected override void EmitCode(Context ctx, bool mustReturn)
        {
            var gen = ctx.CurrentMethod.Generator;

            gen.EmitJump(_destinationLabel);
        }

        #endregion
    }
}