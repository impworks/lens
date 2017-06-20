using System;
using Lens.Compiler;

namespace Lens.SyntaxTree.Internals
{
    /// <summary>
    /// Emits a pointer to current object.
    /// </summary>
    internal class ThisNode : NodeBase
    {
        #region Resolve

        protected override Type ResolveInternal(Context ctx, bool mustReturn)
        {
            // sic! compiler error, no need to localize
            if (ctx.CurrentMethod.IsStatic)
                Error("Cannot access self-reference in static context!");

            return ctx.CurrentType.TypeBuilder;
        }

        #endregion

        #region Emit

        protected override void EmitInternal(Context ctx, bool mustReturn)
        {
            var gen = ctx.CurrentMethod.Generator;
            gen.EmitLoadArgument(0);
        }

        #endregion
    }
}