using System;
using Lens.Compiler;
using Lens.Resolver;

namespace Lens.SyntaxTree.Operators.TypeBased
{
    /// <summary>
    /// Checks if the object is of given type.
    /// </summary>
    internal class IsOperatorNode : TypeCheckOperatorNodeBase
    {
        #region Resolve

        protected override TypeEntry ResolveInternal(Context ctx, bool mustReturn = true)
        {
            return TypeEntryCache.Of<bool>();
        }

        #endregion

        #region Emit

        protected override void EmitInternal(Context ctx, bool mustReturn)
        {
            var gen = ctx.CurrentMethod.Generator;

            var exprType = Expression.Resolve(ctx);
            var desiredType = Type != null ? TypeEntryCache.Of(Type) : ctx.ResolveType(TypeSignature);

            CheckTypeInSafeMode(ctx, desiredType);

            // types are identical
            if (exprType == desiredType)
            {
                gen.EmitConstant(true);
                return;
            }

            // valuetype can only be cast to object
            if (exprType.IsValueType)
            {
                gen.EmitConstant(desiredType.Is<object>());
                return;
            }

            Expression.Emit(ctx, true);

            // check if not null
            if (desiredType.Is<object>())
            {
                gen.EmitNull();
                gen.EmitCompareEqual();
                gen.EmitConstant(false);
                gen.EmitCompareEqual();
            }
            else
            {
                gen.EmitCast(desiredType.Materialize(), false);
                gen.EmitNull();
                gen.EmitCompareGreater(false);
            }
        }

        #endregion
    }
}