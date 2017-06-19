using System;
using System.Collections.Generic;
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
        public Type Type;

        /// <summary>
        /// Desired size of the array (must be int!).
        /// </summary>
        public NodeBase Size;

        #endregion

        #region Resolve

        protected override Type resolve(Context ctx, bool mustReturn)
        {
            if (Type == null)
                Type = ctx.ResolveType(TypeSignature);

            var idxType = Size.Resolve(ctx);
            if (!typeof(int).IsExtendablyAssignableFrom(idxType))
                Error(Size, CompilerMessages.ArraySizeNotInt, idxType);

            return Type.MakeArrayType();
        }

        #endregion

        #region Transform

        protected override IEnumerable<NodeChild> GetChildren()
        {
            yield return new NodeChild(Size, x => Size = x);
        }

        #endregion

        #region Emit

        protected override void EmitCode(Context ctx, bool mustReturn)
        {
            var gen = ctx.CurrentMethod.Generator;

            Expr.Cast<int>(Size).Emit(ctx, true);
            gen.EmitCreateArray(Type);
        }

        #endregion

        #region Debug

        protected bool Equals(NewObjectArrayNode other)
        {
            return Equals(TypeSignature, other.TypeSignature) && Type == other.Type && Equals(Size, other.Size);
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
                hashCode = (hashCode * 397) ^ (Size != null ? Size.GetHashCode() : 0);
                return hashCode;
            }
        }

        #endregion
    }
}