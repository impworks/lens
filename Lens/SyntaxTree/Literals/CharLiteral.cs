using Lens.Compiler;

namespace Lens.SyntaxTree.Literals
{
    /// <summary>
    /// A node representing a single unicode character.
    /// </summary>
    internal class CharNode : LiteralNodeBase<char>
    {
        #region Constructor

        public CharNode(char value)
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