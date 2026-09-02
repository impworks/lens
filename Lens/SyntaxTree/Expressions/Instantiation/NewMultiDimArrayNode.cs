using System.Collections.Generic;
using System.Linq;
using Lens.Compiler;
using Lens.Resolver;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.Expressions.Instantiation
{
    /// <summary>
    /// A node representing a multidimensional array literal: 'new @[[1; 2]; [3; 4]]'.
    ///
    /// The literal is written as a tree of nested rows, and its rank is how deeply the rows nest.
    /// The array itself is rectangular, so every row at a given level must have the same length as
    /// its neighbours, which is what binding checks before anything is emitted.
    /// </summary>
    internal class NewMultiDimArrayNode : NodeBase
    {
        #region Fields

        /// <summary>
        /// The literal as it was written: each item is either a <see cref="NodeBase"/> leaf or a
        /// nested row, itself a list of items.
        /// </summary>
        public List<object> Items = new List<object>();

        /// <summary>
        /// The leaves in row-major order, which is the order the array is filled in.
        /// </summary>
        private List<NodeBase> _expressions;

        /// <summary>
        /// The length of each dimension, outermost first.
        /// </summary>
        private List<int> _dimensions;

        /// <summary>
        /// Common type inferred from all items' actual types.
        /// </summary>
        private TypeEntry _itemType;

        private List<NodeBase> Expressions => _expressions ?? (_expressions = Flatten(Items).ToList());

        #endregion

        #region Resolve

        protected override TypeEntry ResolveInternal(Context ctx, bool mustReturn)
        {
            _dimensions = new List<int>();
            MeasureRow(Items, 0);

            if (Expressions.Count == 0)
                Error(CompilerMessages.ArrayEmpty);

            _itemType = ResolveItemType(ctx);

            if (_itemType.Is<NullType>())
                Error(CompilerMessages.ArrayTypeUnknown);

            // every element is checked here rather than while emitting, because an editor binds
            // the tree and never emits: a check that lives in EmitInternal is one the reader of a
            // half-written script never sees
            foreach (var curr in Expressions)
            {
                var currType = ctx.CheckTypedExpression(curr, allowNull: true);

                if (!_itemType.IsExtendablyAssignableFrom(ctx.Resolver, currType))
                    Error(curr, CompilerMessages.ArrayElementTypeMismatch, currType, _itemType);
            }

            return _itemType.MakeArray(ctx.Resolver, _dimensions.Count);
        }

        /// <summary>
        /// Walks one row of the literal, recording the length of its level and checking that it
        /// agrees with the rows already seen at the same depth.
        /// </summary>
        private void MeasureRow(List<object> row, int depth)
        {
            if (_dimensions.Count == depth)
                _dimensions.Add(row.Count);
            else if (_dimensions[depth] != row.Count)
                Error(CompilerMessages.MultiDimArrayNotRectangular, _dimensions[depth], row.Count);

            var nested = row.Count > 0 && row[0] is List<object>;

            foreach (var curr in row)
            {
                var group = curr as List<object>;

                if ((group != null) != nested)
                    Error(
                        curr as NodeBase ?? this,
                        CompilerMessages.MultiDimArrayRaggedNesting,
                        group == null ? depth + 1 : depth + 2,
                        nested ? depth + 2 : depth + 1
                    );

                if (group != null)
                    MeasureRow(group, depth + 1);
            }
        }

        /// <summary>
        /// Infers the element type from every leaf of the literal.
        /// </summary>
        private TypeEntry ResolveItemType(Context ctx)
        {
            try
            {
                // a lambda literal among the items has to settle before the items can be compared:
                // the array stores it as the delegate it becomes, and nothing here names one
                var types = Expressions.Select(n => ctx.SettleLambda(n)).ToArray();
                return types.GetMostCommonType(ctx.Resolver);
            }
            catch (LensCompilerException ex)
            {
                // the items are resolved here too, and whatever goes wrong inside one of them is
                // already bound to the item that has it
                if (ex.StartLocation == null || ex.EndLocation == null)
                    ex.BindToLocation(this);

                throw;
            }
        }

        #endregion

        #region Transform

        internal override IEnumerable<NodeChild> GetChildren()
        {
            return Expressions.Select(x => new NodeChild(x));
        }

        internal override IReadOnlyList<NodeBase> Operands => Expressions;

        internal override NodeBase WithOperands(IReadOnlyList<NodeBase> operands)
        {
            var copy = Copy<NewMultiDimArrayNode>();
            var queue = new Queue<NodeBase>(operands);
            copy.Items = Rebuild(Items, queue);
            return copy;
        }

        #endregion

        #region Emit

        protected override void EmitInternal(Context ctx, bool mustReturn)
        {
            var gen = ctx.CurrentMethod.Generator;
            var arrayType = Resolve(ctx);
            var materialized = arrayType.Materialize();
            var tmpVar = ctx.Scope.DeclareImplicit(ctx, arrayType, true);
            var itemType = _itemType.Materialize();

            foreach (var curr in _dimensions)
                gen.EmitConstant(curr);

            MultiDimArrays.EmitCreate(ctx, materialized);
            gen.EmitSaveLocal(tmpVar.LocalBuilder);

            var setter = MultiDimArrays.SetterOf(ctx, materialized);
            var indexes = new int[_dimensions.Count];

            foreach (var curr in Expressions)
            {
                gen.EmitLoadLocal(tmpVar.LocalBuilder);

                foreach (var idx in indexes)
                    gen.EmitConstant(idx);

                Expr.Cast(curr, itemType).Emit(ctx, true);
                gen.EmitCall(setter);

                Advance(indexes);
            }

            gen.EmitLoadLocal(tmpVar.LocalBuilder);
        }

        /// <summary>
        /// Steps the index tuple on to the next cell in row-major order.
        /// </summary>
        private void Advance(int[] indexes)
        {
            for (var idx = indexes.Length - 1; idx >= 0; idx--)
            {
                indexes[idx]++;
                if (indexes[idx] < _dimensions[idx])
                    return;

                indexes[idx] = 0;
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Walks the literal's leaves in row-major order.
        /// </summary>
        private static IEnumerable<NodeBase> Flatten(List<object> row)
        {
            foreach (var curr in row)
            {
                if (curr is List<object> group)
                {
                    foreach (var leaf in Flatten(group))
                        yield return leaf;
                }
                else
                {
                    yield return (NodeBase) curr;
                }
            }
        }

        /// <summary>
        /// Rebuilds the literal's shape around a new set of leaves, taken in row-major order.
        /// </summary>
        private static List<object> Rebuild(List<object> row, Queue<NodeBase> leaves)
        {
            var result = new List<object>(row.Count);

            foreach (var curr in row)
                result.Add(curr is List<object> group ? (object) Rebuild(group, leaves) : leaves.Dequeue());

            return result;
        }

        /// <summary>
        /// The nesting of the literal, as a flat sequence of row lengths in walk order: two
        /// literals with the same leaves but different shapes are different arrays.
        /// </summary>
        private static IEnumerable<int> Shape(List<object> row)
        {
            yield return row.Count;

            foreach (var curr in row)
                if (curr is List<object> group)
                    foreach (var nested in Shape(group))
                        yield return nested;
        }

        #endregion

        #region Debug

        protected bool Equals(NewMultiDimArrayNode other)
        {
            return Expressions.SequenceEqual(other.Expressions)
                   && Shape(Items).SequenceEqual(Shape(other.Items));
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((NewMultiDimArrayNode) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = 0;

                foreach (var curr in Expressions)
                    hashCode = (hashCode * 397) ^ (curr != null ? curr.GetHashCode() : 0);

                return hashCode;
            }
        }

        public override string ToString()
        {
            return string.Format("mdarray({0})", Describe(Items));
        }

        private static string Describe(List<object> row)
        {
            return "[" + string.Join(";", row.Select(x => x is List<object> group ? Describe(group) : x.ToString())) + "]";
        }

        #endregion
    }
}
