using Lens.Compiler;

namespace Lens.SyntaxTree.Internals
{
    /// <summary>
    /// Marks a position in a lowered body that a jump can name.
    /// </summary>
    internal class LabelNode : NodeBase, IMetaNode
    {
        #region Constructor

        public LabelNode(LabelRef label)
        {
            Label = label;
        }

        #endregion

        #region Fields

        public readonly LabelRef Label;

        #endregion

        #region Emit

        protected override void EmitInternal(Context ctx, bool mustReturn)
        {
            var gen = ctx.CurrentMethod.Generator;
            gen.MarkLabel(Label.Resolve(gen));
        }

        #endregion

        #region Debug

        public override string ToString()
        {
            return $"{Label}:";
        }

        #endregion
    }
}
