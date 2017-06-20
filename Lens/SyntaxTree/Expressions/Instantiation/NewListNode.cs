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
    /// A node representing a new List declaration.
    /// </summary>
    internal class NewListNode : CollectionNodeBase<NodeBase>
    {
        #region Fields

        /// <summary>
        /// Common type inferred from all items' actual types.
        /// </summary>
        private Type _itemType;

        #endregion

        #region Resolve

        protected override Type ResolveInternal(Context ctx, bool mustReturn)
        {
            if (Expressions.Count == 0)
                Error(CompilerMessages.ListEmpty);

            _itemType = ResolveItemType(Expressions, ctx);
            if (_itemType == typeof(NullType))
                Error(CompilerMessages.ListTypeUnknown);

            return typeof(List<>).MakeGenericType(_itemType);
        }

        #endregion

        #region Transform

        protected override IEnumerable<NodeChild> GetChildren()
        {
            return Expressions.Select((expr, i) => new NodeChild(expr, x => Expressions[i] = x));
        }

        #endregion

        #region Emit

        protected override void EmitInternal(Context ctx, bool mustReturn)
        {
            var gen = ctx.CurrentMethod.Generator;
            var tmpVar = ctx.Scope.DeclareImplicit(ctx, Resolve(ctx), true);

            var listType = Resolve(ctx);
            var ctor = ctx.ResolveConstructor(listType, new[] {typeof(int)});
            var addMethod = ctx.ResolveMethod(listType, "Add", new[] {_itemType});

            var count = Expressions.Count;
            gen.EmitConstant(count);
            gen.EmitCreateObject(ctor.ConstructorInfo);
            gen.EmitSaveLocal(tmpVar.LocalBuilder);

            foreach (var curr in Expressions)
            {
                var currType = curr.Resolve(ctx);

                ctx.CheckTypedExpression(curr, currType, true);

                if (!_itemType.IsExtendablyAssignableFrom(currType))
                    Error(curr, CompilerMessages.ListElementTypeMismatch, currType, _itemType);

                gen.EmitLoadLocal(tmpVar.LocalBuilder);

                Expr.Cast(curr, addMethod.ArgumentTypes[0]).Emit(ctx, true);
                gen.EmitCall(addMethod.MethodInfo, addMethod.IsVirtual);
            }

            gen.EmitLoadLocal(tmpVar.LocalBuilder);
        }

        #endregion

        #region Debug

        public override string ToString()
        {
            return string.Format("list({0})", string.Join(";", Expressions));
        }

        #endregion
    }
}