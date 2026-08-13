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

        public GotoNode(LabelRef label, NodeBase condition = null, bool jumpIfTrue = true, bool isLeave = false)
        {
            Label = label;
            Condition = condition;
            JumpIfTrue = jumpIfTrue;
            IsLeave = isLeave;
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

        /// <summary>
        /// Whether the jump leaves a protected region.
        ///
        /// A plain branch out of a try is not valid IL; 'leave' is, and outside a protected region
        /// it does exactly what a branch does. A suspension therefore always leaves, wherever in
        /// the body it happens to be.
        /// </summary>
        public readonly bool IsLeave;

        #endregion

        #region Transform

        internal override IEnumerable<NodeChild> GetChildren()
        {
            if (Condition != null)
                yield return new NodeChild(Condition, true);
        }

        #endregion

        #region Emit

        protected override void EmitInternal(Context ctx, bool mustReturn)
        {
            var gen = ctx.CurrentMethod.Generator;
            var label = Label.Resolve(gen);

            if (Condition == null)
            {
                if (IsLeave)
                    gen.EmitLeave(label);
                else
                    gen.EmitJump(label);

                return;
            }

            var condType = Condition.Resolve(ctx);
            if (!condType.IsExtendablyAssignableFrom(ctx.Resolver, TypeEntryCache.Of<bool>()))
                Error(Condition, CompilerMessages.ConditionTypeMismatch, condType);

            Expr.Cast(Condition, typeof(bool)).Emit(ctx, true);

            if (IsLeave)
            {
                // a conditional leave has no opcode of its own: branch over an unconditional one
                var skip = gen.DefineLabel();

                if (JumpIfTrue)
                    gen.EmitBranchFalse(skip);
                else
                    gen.EmitBranchTrue(skip);

                gen.EmitLeave(label);
                gen.MarkLabel(skip);
                return;
            }

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
