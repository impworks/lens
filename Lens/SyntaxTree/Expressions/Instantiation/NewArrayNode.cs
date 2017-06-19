using System;
using System.Collections.Generic;
using System.Linq;
using Lens.Compiler;
using Lens.Resolver;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.Expressions.Instantiation
{
    /// <summary>
    /// A node representing a new array declaration.
    /// </summary>
    internal class NewArrayNode : CollectionNodeBase<NodeBase>
    {
        #region Fields

        /// <summary>
        /// Common type for all collection items.
        /// </summary>
        private Type _itemType;

        #endregion

        #region Resolve

        protected override Type resolve(Context ctx, bool mustReturn)
        {
            if (Expressions.Count == 0)
                Error(CompilerMessages.ArrayEmpty);

            _itemType = ResolveItemType(Expressions, ctx);

            if (_itemType == typeof(NullType))
                Error(CompilerMessages.ArrayTypeUnknown);

            return _itemType.MakeArrayType();
        }

        #endregion

        #region Transform

        protected override IEnumerable<NodeChild> GetChildren()
        {
            return Expressions.Select((expr, i) => new NodeChild(expr, x => Expressions[i] = x));
        }

        #endregion

        #region Emit

        protected override void EmitCode(Context ctx, bool mustReturn)
        {
            var gen = ctx.CurrentMethod.Generator;
            var tmpVar = ctx.Scope.DeclareImplicit(ctx, Resolve(ctx), true);

            // create array
            var count = Expressions.Count;
            gen.EmitConstant(count);
            gen.EmitCreateArray(_itemType);
            gen.EmitSaveLocal(tmpVar.LocalBuilder);

            for (var idx = 0; idx < count; idx++)
            {
                var currType = Expressions[idx].Resolve(ctx);

                ctx.CheckTypedExpression(Expressions[idx], currType, true);

                if (!_itemType.IsExtendablyAssignableFrom(currType))
                    Error(Expressions[idx], CompilerMessages.ArrayElementTypeMismatch, currType, _itemType);

                gen.EmitLoadLocal(tmpVar.LocalBuilder);
                gen.EmitConstant(idx);

                var cast = Expr.Cast(Expressions[idx], _itemType);

                if (_itemType.IsValueType)
                {
                    gen.EmitLoadIndex(_itemType, true);
                    cast.Emit(ctx, true);
                    gen.EmitSaveObject(_itemType);
                }
                else
                {
                    cast.Emit(ctx, true);
                    gen.EmitSaveIndex(_itemType);
                }
            }

            gen.EmitLoadLocal(tmpVar.LocalBuilder);
        }

        #endregion

        #region Debug

        public override string ToString()
        {
            return string.Format("array({0})", string.Join(";", Expressions));
        }

        #endregion
    }
}