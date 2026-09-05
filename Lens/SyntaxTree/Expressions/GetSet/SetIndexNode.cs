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
        /// Whether the type has no index setter and the value is stored through the managed
        /// pointer the getter returns, in which case <see cref="_indexer"/> is that getter.
        /// </summary>
        private bool _writesThroughRef;

        /// <summary>
        /// How an index of System.Index or System.Range is to be resolved against the length of
        /// what is being indexed, or null when the index is an ordinary one.
        /// </summary>
        private IndexAccess _access;

        /// <summary>
        /// Value to be assigned.
        /// </summary>
        public NodeBase Value { get; set; }

        #endregion

        #region Resolve

        protected override TypeEntry ResolveInternal(Context ctx, bool mustReturn)
        {
            var exprType = Expression.Resolve(ctx);

            // an index of System.Index or System.Range is not an index the target itself
            // understands: a single element is stored where an integer would have stored it, and a
            // whole segment is replaced by the sequence of values assigned to it
            _access = IndexAccess.Detect(ctx, exprType, Indexes, isGetter: false, owner: this);
            if (_access != null)
            {
                EnsureLambdaInferred(ctx, Value, _access.ResultType);

                var assigned = Value.Resolve(ctx);
                if (!_access.ResultType.IsExtendablyAssignableFrom(ctx.Resolver, assigned))
                    Error(Value, CompilerMessages.ImplicitCastImpossible, assigned, _access.ResultType);

                return base.ResolveInternal(ctx, mustReturn);
            }

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
                    // an indexer whose getter returns a managed pointer needs no setter: it hands
                    // back the location of the element, and the assignment stores into it. This is
                    // the only way a Span's element is written.
                    _indexer = TryResolveByRefGetter(ctx, exprType, idxTypes);
                    if (_indexer == null)
                    {
                        ex.BindToLocation(this);
                        throw;
                    }

                    _writesThroughRef = true;
                }
            }

            var valDestType = exprType.IsArray
                ? exprType.ElementType
                : (_writesThroughRef
                    ? _indexer.ReturnType.ElementType
                    : _indexer.ArgumentTypes[_indexer.ArgumentTypes.Length - 1]);

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

        /// <summary>
        /// Looks for an index getter that returns a managed pointer, which can stand in for the
        /// setter the type does not have. Returns null when there is none, so that the error the
        /// missing setter caused is the one reported.
        /// </summary>
        private static MethodWrapper TryResolveByRefGetter(Context ctx, TypeEntry exprType, TypeEntry[] idxTypes)
        {
            try
            {
                var getter = ctx.ResolveIndexer(exprType, idxTypes, true);
                return getter.ReturnType.IsByRef ? getter : null;
            }
            catch (LensCompilerException)
            {
                return null;
            }
        }

        #endregion

        #region Transform

        protected override NodeBase Expand(Context ctx, bool mustReturn)
        {
            return _access?.ExpandSet(ctx, this);
        }

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
                var valDest = _writesThroughRef
                    ? _indexer.ReturnType.ElementType
                    : _indexer.ArgumentTypes[_indexer.ArgumentTypes.Length - 1];

                // an indexer of a value type is an instance method like any other, and needs the
                // receiver's address rather than a copy of it - as does one reached through a
                // constraint of a type parameter, which is called under the 'constrained.' prefix
                Expression.EmitNodeForAccess(ctx, _indexer.ConstrainedTo != null);

                for (var idx = 0; idx < Indexes.Count; idx++)
                    Expr.Cast(Indexes[idx], _indexer.ArgumentTypes[idx].Materialize()).Emit(ctx, true);

                // the location has to be under the value on the stack, so the getter is called
                // before the value is evaluated
                if (_writesThroughRef)
                    gen.EmitCall(_indexer.MethodInfo, _indexer.IsVirtual, _indexer.ConstrainedTo?.Materialize());

                Expr.Cast(Value, valDest.Materialize()).Emit(ctx, true);

                if (_writesThroughRef)
                    gen.EmitSaveObject(valDest.Materialize());
                else
                    gen.EmitCall(_indexer.MethodInfo, _indexer.IsVirtual, _indexer.ConstrainedTo?.Materialize());
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