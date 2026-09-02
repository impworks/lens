using System;
using System.Collections.Generic;
using System.Linq;
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

        protected override TypeEntry ResolveInternal(Context ctx, bool mustReturn)
        {
            var exprType = Expression.Resolve(ctx);
            var idxTypes = Indexes.Select(x => x.Resolve(ctx)).ToArray();

            if (exprType.IsArray)
            {
                if (Indexes.Count != exprType.ArrayRank)
                    Error(CompilerMessages.ArrayRankMismatch, exprType, exprType.ArrayRank, Indexes.Count);
            }
            else
            {
                try
                {
                    _indexer = ctx.ResolveIndexer(exprType, idxTypes, false);
                }
                catch (LensCompilerException ex)
                {
                    ex.BindToLocation(this);
                    throw;
                }
            }

            var valDestType = exprType.IsArray ? exprType.ElementType : _indexer.ArgumentTypes[_indexer.ArgumentTypes.Length - 1];

            for (var idx = 0; idx < idxTypes.Length; idx++)
            {
                var idxDestType = exprType.IsArray ? TypeEntryCache.Of<int>() : _indexer.ArgumentTypes[idx];
                if (!idxDestType.IsExtendablyAssignableFrom(ctx.Resolver, idxTypes[idx]))
                    Error(Indexes[idx], CompilerMessages.ImplicitCastImpossible, idxTypes[idx], idxDestType);
            }

            EnsureLambdaInferred(ctx, Value, valDestType);
            var valType = Value.Resolve(ctx);
            if (!valDestType.IsExtendablyAssignableFrom(ctx.Resolver, valType))
                Error(Value, CompilerMessages.ImplicitCastImpossible, valType, valDestType);

            return base.ResolveInternal(ctx, mustReturn);
        }

        #endregion

        #region Transform

        internal override IEnumerable<NodeChild> GetChildren()
        {
            yield return new NodeChild(Expression);

            foreach (var curr in Indexes)
                yield return new NodeChild(curr);

            yield return new NodeChild(Value);
        }

        internal override IReadOnlyList<NodeBase> Operands => new[] {Expression}.Concat(Indexes).Concat(new[] {Value}).ToArray();

        /// <summary>
        /// The object being indexed into is not a value the node consumes: were it evaluated ahead
        /// of time and kept in a temporary, a struct would be copied there and the assignment would
        /// land in the copy.
        /// </summary>
        internal override bool CanHoistOperand(int index)
        {
            return index != 0;
        }

        internal override NodeBase WithOperands(IReadOnlyList<NodeBase> operands)
        {
            var copy = Copy<SetIndexNode>();
            copy.Expression = operands[0];
            copy.Indexes = operands.Skip(1).Take(operands.Count - 2).ToList();
            copy.Value = operands[operands.Count - 1];
            return copy;
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
            var itemType = exprType.ElementType.Materialize();

            Expression.Emit(ctx, true);

            foreach (var curr in Indexes)
                Expr.Cast(curr, typeof(int)).Emit(ctx, true);

            Expr.Cast(Value, itemType).Emit(ctx, true);

            if (exprType.ArrayRank == 1)
            {
                gen.EmitSaveIndex(itemType);
                return;
            }

            gen.EmitCall(MultiDimArrays.SetterOf(ctx, exprType.Materialize()));
        }

        /// <summary>
        /// Invokes the object's custom indexer setter.
        /// </summary>
        private void EmitCustom(Context ctx)
        {
            var gen = ctx.CurrentMethod.Generator;

            try
            {
                var valDest = _indexer.ArgumentTypes[_indexer.ArgumentTypes.Length - 1];

                Expression.Emit(ctx, true);

                for (var idx = 0; idx < Indexes.Count; idx++)
                    Expr.Cast(Indexes[idx], _indexer.ArgumentTypes[idx].Materialize()).Emit(ctx, true);

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
            return string.Format("setidx({0} of {1} = {2})", string.Join(";", Indexes), Expression, Value);
        }

        #endregion
    }
}