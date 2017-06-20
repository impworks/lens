using Lens.Compiler;

namespace Lens.SyntaxTree.Literals
{
    /// <summary>
    /// A node representing an integer literal.
    /// </summary>
    internal class IntNode : LiteralNodeBase<int>
    {
        #region Constructor

        public IntNode(int value = 0)
        {
            Value = value;
        }

        #endregion

        #region Emit

        protected override void EmitInternal(Context ctx, bool mustReturn)
        {
            var gen = ctx.CurrentMethod.Generator;
            gen.EmitConstant(Value);
        }

        #endregion
    }
}