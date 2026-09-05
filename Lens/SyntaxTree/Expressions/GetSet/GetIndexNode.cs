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
    /// A node representing a read-access to an array or list's value.
    /// </summary>
    internal class GetIndexNode : IndexNodeBase, IPointerProvider
    {
        #region Fields

        /// <summary>
        /// Cached property information.
        /// </summary>
        private MethodWrapper _getter;

        /// <summary>
        /// How an index of System.Index or System.Range is to be resolved against the length of
        /// what is being indexed, or null when the index is an ordinary one.
        /// </summary>
        private IndexAccess _access;

        public bool RefArgumentRequired { get; set; }

        /// <summary>
        /// The indexer's getter binding settled on, or null when the access is to an array.
        /// </summary>
        internal MethodWrapper BoundGetter => _getter;

        #endregion

        #region Resolve

        protected override TypeEntry ResolveInternal(Context ctx, bool mustReturn)
        {
            var exprType = Expression.Resolve(ctx);

            // an index of System.Index or System.Range is not an index the target itself
            // understands, and the access it really makes is the one an integer would have made
            _access = IndexAccess.Detect(ctx, exprType, Indexes, isGetter: true, owner: this);
            if (_access != null)
                return _access.ResultType;

            if (exprType.IsArray)
            {
                CheckArrayRank(exprType);
                return exprType.ElementType;
            }

            var idxTypes = Indexes.Select(x => x.Resolve(ctx)).ToArray();
            try
            {
                _getter = ctx.ResolveIndexer(exprType, idxTypes, true);

            // what may be passed by reference is decided here rather than while emitting, because
            // an editor binds the tree and never emits: a check that lives in EmitInternal is one
            // the reader of a half-written script never sees
                // an indexer's getter hands back a copy, and there is no storage behind it for a
                // callee to write into - unless it returns a managed pointer, which is a location
                // and nothing else
                if (RefArgumentRequired && _getter.ReturnType.IsValueType && !_getter.ReturnType.IsByRef)
                    Error(CompilerMessages.IndexerValuetypeRef, exprType, _getter.ReturnType);

                return _getter.ReturnType.Dereferenced();
            }
            catch (LensCompilerException ex)
            {
                ex.BindToLocation(this);
                throw;
            }
        }

        /// <summary>
        /// Reports an index list that does not match the array's number of dimensions.
        ///
        /// This is checked while binding rather than while emitting, because an editor binds the
        /// tree and never emits: a check that lives in EmitInternal is one the reader of a
        /// half-written script never sees.
        /// </summary>
        private void CheckArrayRank(TypeEntry arrayType)
        {
            if (Indexes.Count != arrayType.ArrayRank)
                Error(CompilerMessages.ArrayRankMismatch, arrayType, arrayType.ArrayRank, Indexes.Count);
        }

        #endregion

        #region Transform

        protected override NodeBase Expand(Context ctx, bool mustReturn)
        {
            if (_access != null)
                return _access.ExpandGet(ctx, this);

            var indexes = IndexesWithDefaults(_getter);
            if (indexes == null)
                return null;

            return new GetIndexNode
            {
                Expression = Expression,
                Indexes = indexes,
                IsNullSafe = IsNullSafe,
                RefArgumentRequired = RefArgumentRequired,
                StartLocation = StartLocation,
                EndLocation = EndLocation
            };
        }

        internal override IEnumerable<NodeChild> GetChildren()
        {
            yield return new NodeChild(Expression);

            foreach (var curr in Indexes)
                yield return new NodeChild(curr);
        }

        internal override IReadOnlyList<NodeBase> Operands => new[] {Expression}.Concat(Indexes).ToArray();

        internal override NodeBase WithOperands(IReadOnlyList<NodeBase> operands)
        {
            var copy = Copy<GetIndexNode>();
            copy.Expression = operands[0];
            copy.Indexes = operands.Skip(1).ToList();
            return copy;
        }

        #endregion

        #region Emit

        protected override void EmitInternal(Context ctx, bool mustReturn)
        {
            if (_getter == null)
                EmitArray(ctx);
            else
                EmitCustom(ctx);
        }

        /// <summary>
        /// Emits the code for retrieving an array item by index.
        /// </summary>
        private void EmitArray(Context ctx)
        {
            var gen = ctx.CurrentMethod.Generator;

            var exprType = Expression.Resolve(ctx);
            var itemType = exprType.ElementType.Materialize();
            var needsPointer = RefArgumentRequired || ctx.IsPointerRequired(this);

            Expression.Emit(ctx, true);

            foreach (var curr in Indexes)
                Expr.Cast(curr, typeof(int)).Emit(ctx, true);

            if (exprType.ArrayRank == 1)
            {
                gen.EmitLoadIndex(itemType, needsPointer);
                return;
            }

            var arrayType = exprType.Materialize();
            gen.EmitCall(
                needsPointer
                    ? MultiDimArrays.AddressOf(ctx, arrayType)
                    : MultiDimArrays.GetterOf(ctx, arrayType)
            );
        }

        /// <summary>
        /// Emits the code for retrieving a value from an object by custom indexer.
        /// </summary>
        private void EmitCustom(Context ctx)
        {
            var gen = ctx.CurrentMethod.Generator;

            var ptrExpr = Expression as IPointerProvider;
            if (ptrExpr != null)
            {
                if (ctx.IsPointerRequired(this))
                    ctx.RequirePointer(ptrExpr);

                ptrExpr.RefArgumentRequired = RefArgumentRequired;
            }

            // an indexer of a value type is an instance method like any other, and needs the
            // receiver's address rather than a copy of it - as does one reached through a
            // constraint of a type parameter, which is called under the 'constrained.' prefix
            Expression.EmitNodeForAccess(ctx, _getter.ConstrainedTo != null);

            for (var idx = 0; idx < Indexes.Count; idx++)
                Expr.Cast(Indexes[idx], _getter.ArgumentTypes[idx].Materialize()).Emit(ctx, true);

            gen.EmitCall(_getter.MethodInfo, _getter.IsVirtual, _getter.ConstrainedTo?.Materialize());

            // a getter that returns a managed pointer has left the element's location on the
            // stack, which is exactly what a caller asking for an address wants; anything else
            // wants the value at it
            var returnType = _getter.ReturnType;
            if (returnType.IsByRef && !RefArgumentRequired && !ctx.IsPointerRequired(this))
                gen.EmitLoadFromPointer(returnType.ElementType.Materialize());
        }

        #endregion

        #region Debug

        protected bool Equals(GetIndexNode other)
        {
            return base.Equals(other)
                   && RefArgumentRequired.Equals(other.RefArgumentRequired);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((GetIndexNode) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = base.GetHashCode();
                hash = (hash * 397) ^ RefArgumentRequired.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            return string.Format("getidx({0} of {1})", string.Join(";", Indexes), Expression);
        }

        #endregion
    }
}