using Lens.Compiler;

namespace Lens.SyntaxTree.Literals
{
    /// <summary>
    /// A node representing a single-precision floating point number literal.
    /// </summary>
    internal class FloatNode : LiteralNodeBase<float>
    {
        #region Constructor

        public FloatNode(float value = 0)
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