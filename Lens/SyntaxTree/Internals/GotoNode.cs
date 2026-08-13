using System.Collections.Generic;
using Lens.Compiler;
using Lens.Resolver;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.Internals
{
    /// <summary>
    /// Jumps to a label, either unconditionally or on the value of a condition.
    ///
    /// This is compiler-internal vocabulary produced by the lowering pass. LENS has no goto: there
    /// is no grammar that reaches this node.
    /// </summary>
    internal class GotoNode : NodeBase, IMetaNode
    {
        #region Constructor

        public GotoNode(LabelRef label, NodeBase condition = null, bool jumpIfTrue = true)
        {
            Label = label;
            Condition = condition;
            JumpIfTrue = jumpIfTrue;
        }

        #endregion

        #region Fields

        public readonly LabelRef Label;

        /// <summary>
        /// The condition to test, or null for an unconditional jump.
        /// </summary>
        public readonly NodeBase Condition;

        /// <summary>
        /// Whether the jump is taken when the condition holds or when it does not.
        /// </summary>
        public readonly bool JumpIfTrue;

        #endregion

        #region Transform

        internal override IEnumerable<NodeChild> GetChildren()
        {
            if (Condition != null)
                yield return new NodeChild(Condition);
        }

        #endregion

        #region Emit

        protected override void EmitInternal(Context ctx, bool mustReturn)
        {
            var gen = ctx.CurrentMethod.Generator;
            var label = Label.Resolve(gen);

            if (Condition == null)
            {
                gen.EmitJump(label);
                return;
            }

            var condType = Condition.Resolve(ctx);
            if (!condType.IsExtendablyAssignableFrom(ctx.Resolver, TypeEntryCache.Of<bool>()))
                Error(Condition, CompilerMessages.ConditionTypeMismatch, condType);

            Expr.Cast(Condition, typeof(bool)).Emit(ctx, true);

            if (JumpIfTrue)
                gen.EmitBranchTrue(label);
            else
                gen.EmitBranchFalse(label);
        }

        #endregion

        #region Debug

        public override string ToString()
        {
            if (Condition == null)
                return $"goto {Label}";

            return $"goto {Label} {(JumpIfTrue ? "if" : "unless")} {Condition}";
        }

        #endregion
    }
}
