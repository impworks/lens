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
        private TypeEntry _itemType;

        #endregion

        #region Resolve

        protected override TypeEntry ResolveInternal(Context ctx, bool mustReturn)
        {
            if (Expressions.Count == 0)
                Error(CompilerMessages.ListEmpty);

            _itemType = ResolveItemType(Expressions, ctx);
            if (_itemType.Is<NullType>())
                Error(CompilerMessages.ListTypeUnknown);

            return TypeEntryCache.Of(typeof(List<>)).MakeGeneric(ctx.Resolver, new[] {_itemType});
        }

        #endregion

        #region Transform

        internal override IEnumerable<NodeChild> GetChildren()
        {
            return Expressions.Select((expr, i) => new NodeChild(expr));
        }

        internal override IReadOnlyList<NodeBase> Operands => Expressions;

        internal override NodeBase WithOperands(IReadOnlyList<NodeBase> operands)
        {
            var copy = Copy<NewListNode>();
            copy.Expressions = operands.ToList();
            return copy;
        }

        #endregion

        #region Emit

        protected override void EmitInternal(Context ctx, bool mustReturn)
        {
            var gen = ctx.CurrentMethod.Generator;
            var tmpVar = ctx.Scope.DeclareImplicit(ctx, Resolve(ctx), true);

            var listType = Resolve(ctx);
            var ctor = ctx.ResolveConstructor(listType, new[] {TypeEntryCache.Of<int>()});
            var addMethod = ctx.ResolveMethod(listType, "Add", new[] {_itemType});

            var count = Expressions.Count;
            gen.EmitConstant(count);
            gen.EmitCreateObject(ctor.ConstructorInfo);
            gen.EmitSaveLocal(tmpVar.LocalBuilder);

            foreach (var curr in Expressions)
            {
                var currType = curr.Resolve(ctx);

                ctx.CheckTypedExpression(curr, currType, true);

                if (!_itemType.IsExtendablyAssignableFrom(ctx.Resolver, currType))
                    Error(curr, CompilerMessages.ListElementTypeMismatch, currType, _itemType);

                gen.EmitLoadLocal(tmpVar.LocalBuilder);

                Expr.Cast(curr, addMethod.ArgumentTypes[0].Materialize()).Emit(ctx, true);
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