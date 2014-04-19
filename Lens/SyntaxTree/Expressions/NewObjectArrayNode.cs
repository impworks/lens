using System;
using Lens.Compiler;
using Lens.Resolver;
using Lens.Translations;

namespace Lens.SyntaxTree.Expressions
{
    /// <summary>
    /// Represents an empty array of specified size.
    /// </summary>
    internal class NewObjectArrayNode : NodeBase
	{
		public TypeSignature TypeSignature;
        public Type Type;

        public NodeBase Size;

        protected override Type resolve(Context ctx, bool mustReturn)
        {
            if (Type == null)
                Type = ctx.ResolveType(TypeSignature);

            var idxType = Size.Resolve(ctx);
            if (!typeof (int).IsExtendablyAssignableFrom(idxType))
                error(Size, CompilerMessages.ArraySizeNotInt, idxType);

            return Type.MakeArrayType();
        }

        protected override void emitCode(Context ctx, bool mustReturn)
        {
            var gen = ctx.CurrentMethod.Generator;

            Expr.Cast<int>(Size).Emit(ctx, true);
            gen.EmitCreateArray(Type);
        }


		#region Equality members

		protected bool Equals(NewObjectArrayNode other)
		{
			return Equals(TypeSignature, other.TypeSignature) && Type == other.Type && Equals(Size, other.Size);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((NewObjectArrayNode)obj);
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
