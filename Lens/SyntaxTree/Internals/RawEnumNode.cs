using System;
using Lens.Compiler;
using Lens.Utils;

namespace Lens.SyntaxTree.Internals
{
    /// <summary>
    /// Emits the enum value as a number.
    /// </summary>
    internal class RawEnumNode : NodeBase
    {
        #region Fields

        /// <summary>
        /// The enum type.
        /// </summary>
        public Type EnumType;

        /// <summary>
        /// The actual value of the enum.
        /// </summary>
        public long Value;

        #endregion

        #region Resolve

        protected override Type ResolveInternal(Context ctx, bool mustReturn)
        {
            return EnumType;
        }

        #endregion

        #region Emit

        protected override void EmitInternal(Context ctx, bool mustReturn)
        {
            var gen = ctx.CurrentMethod.Generator;

            if (Enum.GetUnderlyingType(EnumType).IsAnyOf(typeof(long), typeof(ulong)))
                gen.EmitConstant(Value);
            else
                gen.EmitConstant((int) Value);
        }

        #endregion
    }
}