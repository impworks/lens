using Lens.Compiler;

namespace Lens.SyntaxTree.Literals
{
    /// <summary>
    /// A node representing string literals.
    /// </summary>
    internal class StringNode : LiteralNodeBase<string>
    {
        #region Constructor

        public StringNode(string value = null)
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