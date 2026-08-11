using System;
using System.Collections.Generic;
using Lens.Compiler;
using Lens.Resolver;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.Expressions.GetSet
{
    /// <summary>
    /// A node representing assignment to an index.
    /// </summary>
    internal class SetIndexNode : IndexNodeBase
    {
        #region Fields

        /// <summary>
        /// Wrapper for indexer method (if object is not an array and has a custom indexer defined).
        /// </summary>
        private MethodWrapper _indexer;

        /// <summary>
        /// Value to be assigned.
        /// </summary>
        public NodeBase Value { get; set; }

        #endregion

        #region Resolve

        protected override Type ResolveInternal(Context ctx, bool mustReturn)
        {
            var exprType = Expression.Resolve(ctx);
            var idxType = Index.Resolve(ctx);

            if (!exprType.IsArray)
            {
                try
                {
                    _indexer = ReflectionHelper.ResolveIndexer(ctx.Resolver, exprType, idxType, false);
                }
                catch (LensCompilerException ex)
                {
                    ex.BindToLocation(this);
                    throw;
                }
            }

            var idxDestType = exprType.IsArray ? typeof(int) : _indexer.ArgumentTypes[0].Materialize();
            var valDestType = exprType.IsArray ? exprType.GetElementType() : _indexer.ArgumentTypes[1].Materialize();

            if (!TypeEntryCache.Of(idxDestType).IsExtendablyAssignableFrom(ctx.Resolver, TypeEntryCache.Of(idxType)))
                Error(Index, CompilerMessages.ImplicitCastImpossible, idxType, idxDestType);

            EnsureLambdaInferred(ctx, Value, valDestType);
            var valType = Value.Resolve(ctx);
            if (!TypeEntryCache.Of(valDestType).IsExtendablyAssignableFrom(ctx.Resolver, TypeEntryCache.Of(valType)))
                Error(Value, CompilerMessages.ImplicitCastImpossible, valType, valDestType);

            return base.ResolveInternal(ctx, mustReturn);
        }

        #endregion

        #region Transform

        protected override IEnumerable<NodeChild> GetChildren()
        {
            yield return new NodeChild(Expression);
            yield return new NodeChild(Index);
            yield return new NodeChild(Value);
        }

        #endregion

        #region Emit

        protected override void EmitInternal(Context ctx, bool mustReturn)
        {
            if (_indexer == null)
                EmitArray(ctx);
            else
                EmitCustom(ctx);
        }

        /// <summary>
        /// Saves the value to an array location.
        /// </summary>
        private void EmitArray(Context ctx)
        {
            var gen = ctx.CurrentMethod.Generator;

            var exprType = Expression.Resolve(ctx);
            var itemType = exprType.GetElementType();

            Expression.Emit(ctx, true);
            Expr.Cast(Index, typeof(int)).Emit(ctx, true);
            Expr.Cast(Value, itemType).Emit(ctx, true);
            gen.EmitSaveIndex(itemType);
        }

        /// <summary>
        /// Invokes the object's custom indexer setter.
        /// </summary>
        private void EmitCustom(Context ctx)
        {
            var gen = ctx.CurrentMethod.Generator;

            try
            {
                var idxDest = _indexer.ArgumentTypes[0];
                var valDest = _indexer.ArgumentTypes[1];

                Expression.Emit(ctx, true);

                Expr.Cast(Index, idxDest.Materialize()).Emit(ctx, true);
                Expr.Cast(Value, valDest.Materialize()).Emit(ctx, true);

                gen.EmitCall(_indexer.MethodInfo, _indexer.IsVirtual);
            }
            catch (LensCompilerException ex)
            {
                ex.BindToLocation(this);
                throw;
            }
        }

        #endregion

        #region Debug

        protected bool Equals(SetIndexNode other)
        {
            return base.Equals(other) && Equals(Value, other.Value);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((SetIndexNode) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (base.GetHashCode() * 397) ^ (Value != null ? Value.GetHashCode() : 0);
            }
        }

        public override string ToString()
        {
            return string.Format("setidx({0} of {1} = {2})", Index, Expression, Value);
        }

        #endregion
    }
}