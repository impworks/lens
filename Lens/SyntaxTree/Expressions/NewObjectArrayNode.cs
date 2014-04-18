using System;
using Lens.Compiler;
using Lens.Resolver;
using Lens.Translations;

namespace Lens.SyntaxTree.Expressions
{
    /// <summary>
    /// Represents an empty array of specified size.
    /// </summary>
    internal class NewObjectArrayNode : NodeBase
    {
        public TypeSignature TypeSignature;
        public Type Type;

        public NodeBase Size;

        protected override Type resolve(Context ctx, bool mustReturn)
        {
            if (Type == null)
                Type = ctx.ResolveType(TypeSignature);

            var idxType = Size.Resolve(ctx);
            if (!typeof (int).IsExtendablyAssignableFrom(idxType))
                error(Size, CompilerMessages.ArraySizeNotInt, idxType);

            return Type.MakeArrayType();
        }

        protected override void emitCode(Context ctx, bool mustReturn)
        {
            var gen = ctx.CurrentMethod.Generator;

            Expr.Cast<int>(Size).Emit(ctx, true);
            gen.EmitCreateArray(Type);
        }
    }
}
