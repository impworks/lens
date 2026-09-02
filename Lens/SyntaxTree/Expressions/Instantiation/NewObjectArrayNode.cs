using System.Collections.Generic;
using System.Linq;
using Lens.Compiler;
using Lens.Resolver;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.Expressions.Instantiation
{
    /// <summary>
    /// Represents an empty array of specified size.
    /// </summary>
    internal class NewObjectArrayNode : NodeBase
    {
        #region Fields

        /// <summary>
        /// Raw array item type.
        /// </summary>
        public TypeSignature TypeSignature;

        /// <summary>
        /// Processed array item type.
        /// </summary>
        public TypeEntry Type;

        /// <summary>
        /// Desired length of each dimension (all must be int!).
        /// The array's rank is the number of them.
        /// </summary>
        public List<NodeBase> Sizes = new List<NodeBase>();

        /// <summary>
        /// The single dimension length, for the overwhelmingly common one-dimensional case.
        /// Assigning it replaces the whole list.
        /// </summary>
        public NodeBase Size
        {
            get => Sizes.Count > 0 ? Sizes[0] : null;
            set => Sizes = new List<NodeBase> {value};
        }

        #endregion

        #region Resolve

        protected override TypeEntry ResolveInternal(Context ctx, bool mustReturn)
        {
            if (Type == null)
                Type = ctx.ResolveType(TypeSignature);

            if (Sizes.Count == 0)
                Error(CompilerMessages.ArrayRankNotPositive);

            foreach (var curr in Sizes)
            {
                var idxType = curr.Resolve(ctx);
                if (!TypeEntryCache.Of<int>().IsExtendablyAssignableFrom(ctx.Resolver, idxType))
                    Error(curr, CompilerMessages.ArraySizeNotInt, idxType);
            }

            return Type.MakeArray(ctx.Resolver, Sizes.Count);
        }

        #endregion

        #region Transform

        internal override IEnumerable<NodeChild> GetChildren()
        {
            return Sizes.Select(x => new NodeChild(x));
        }

        internal override IReadOnlyList<NodeBase> Operands => Sizes;

        internal override NodeBase WithOperands(IReadOnlyList<NodeBase> operands)
        {
            var copy = Copy<NewObjectArrayNode>();
            copy.Sizes = operands.ToList();
            return copy;
        }

        #endregion

        #region Emit

        protected override void EmitInternal(Context ctx, bool mustReturn)
        {
            var gen = ctx.CurrentMethod.Generator;

            foreach (var curr in Sizes)
                Expr.Cast<int>(curr).Emit(ctx, true);

            if (Sizes.Count == 1)
            {
                gen.EmitCreateArray(Type.Materialize());
                return;
            }

            MultiDimArrays.EmitCreate(ctx, Resolve(ctx).Materialize());
        }

        #endregion

        #region Debug

        protected bool Equals(NewObjectArrayNode other)
        {
            return Equals(TypeSignature, other.TypeSignature)
                   && Type == other.Type
                   && Sizes.SequenceEqual(other.Sizes);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((NewObjectArrayNode) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = (TypeSignature != null ? TypeSignature.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Type != null ? Type.GetHashCode() : 0);

                foreach (var curr in Sizes)
                    hashCode = (hashCode * 397) ^ (curr != null ? curr.GetHashCode() : 0);

                return hashCode;
            }
        }

        #endregion
    }
}