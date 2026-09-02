using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Lens.Compiler;
using Lens.Resolver;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.Internals
{
    /// <summary>
    /// Opens an enumerator over a sequence, whatever shape of IEnumerable the sequence turns out
    /// to have.
    ///
    /// The lowering pass runs before anything is bound, so it cannot tell an array from a generic
    /// sequence from a bare IEnumerable - and those three want three different casts. This node
    /// carries the question until binding can answer it.
    /// </summary>
    internal class GetEnumeratorNode : NodeBase
    {
        #region Constructor

        public GetEnumeratorNode(NodeBase sequence)
        {
            Sequence = sequence;
        }

        #endregion

        #region Fields

        public readonly NodeBase Sequence;

        /// <summary>
        /// The sequence type the expression must be viewed through to get an enumerator.
        /// </summary>
        private TypeEntry _enumerableType;

        private TypeEntry _enumeratorType;

        #endregion

        #region Resolve

        protected override TypeEntry ResolveInternal(Context ctx, bool mustReturn)
        {
            var seqType = Sequence.Resolve(ctx);

            var elementType = GetElementType(ctx, seqType);
            if (elementType != null)
            {
                _enumerableType = TypeEntry.Generic(ctx.Resolver, typeof(IEnumerable<>), elementType);
                _enumeratorType = TypeEntry.Generic(ctx.Resolver, typeof(IEnumerator<>), elementType);
            }
            else
            {
                _enumerableType = TypeEntryCache.Of<IEnumerable>();
                _enumeratorType = TypeEntryCache.Of<IEnumerator>();
            }

            return _enumeratorType;
        }

        /// <summary>
        /// Finds the item type of a sequence, or null if the sequence is only a bare IEnumerable.
        /// </summary>
        private TypeEntry GetElementType(Context ctx, TypeEntry seqType)
        {
            // only a vector is an IEnumerable<T>; a rank > 1 array has to be read untyped
            if (seqType.IsVectorArray)
                return seqType.ElementType;

            var ifaces = seqType.GetInterfaces(ctx.Resolver);
            if (seqType.IsInterface)
                ifaces = ifaces.Union(new[] {seqType}).ToArray();

            var generic = ifaces.FirstOrDefault(i => i.IsGenericType && i.GetGenericDefinition().Is(typeof(IEnumerable<>)));
            if (generic != null)
                return generic.GenericArguments[0];

            if (!ifaces.Contains(TypeEntryCache.Of<IEnumerable>()))
                Error(Sequence, CompilerMessages.TypeNotIterable, seqType);

            return null;
        }

        #endregion

        #region Transform

        internal override IEnumerable<NodeChild> GetChildren()
        {
            yield return new NodeChild(Sequence);
        }

        protected override NodeBase Expand(Context ctx, bool mustReturn)
        {
            Resolve(ctx);

            return Expr.Invoke(
                Expr.Cast(Sequence, _enumerableType),
                "GetEnumerator"
            );
        }

        #endregion

        #region Debug

        public override string ToString()
        {
            return $"enumerator({Sequence})";
        }

        #endregion
    }
}
