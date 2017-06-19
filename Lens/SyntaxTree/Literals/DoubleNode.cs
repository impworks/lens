using Lens.Compiler;

namespace Lens.SyntaxTree.Literals
{
    /// <summary>
    /// A node representing a double-precision floating point literal.
    /// </summary>
    internal class DoubleNode : LiteralNodeBase<double>
    {
        #region Constructor

        public DoubleNode(double value = 0)
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