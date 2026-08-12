using System;
using Lens.Compiler;
using Lens.Resolver;

namespace Lens.SyntaxTree.Internals
{
    /// <summary>
    /// Emits a pointer to current object.
    /// </summary>
    internal class ThisNode : NodeBase
    {
        #region Resolve

        protected override TypeEntry ResolveInternal(Context ctx, bool mustReturn)
        {
            // sic! compiler error, no need to localize
            if (ctx.CurrentMethod.IsStatic)
                Error("Cannot access self-reference in static context!");

            // for a generic type this is the definition applied to its own parameters,
            // because an open generic type cannot appear in a signature
            return ctx.CurrentType.SelfType;
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