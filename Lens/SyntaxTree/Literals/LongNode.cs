using Lens.Compiler;

namespace Lens.SyntaxTree.Literals
{
    /// <summary>
    /// A node representing a long literal.
    /// </summary>
    internal class LongNode : LiteralNodeBase<long>
    {
        #region Constructor

        public LongNode(long value = 0)
        {
            Value = value;
        }

        #endregion

        #region Emit

        protected override void EmitCode(Context ctx, bool mustReturn)
        {
            var gen = ctx.CurrentMethod.Generator;
            gen.EmitConstant(Value);
        }

        #endregion
    }
}