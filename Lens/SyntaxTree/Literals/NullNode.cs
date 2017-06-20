using System;
using Lens.Compiler;

namespace Lens.SyntaxTree.Literals
{
    /// <summary>
    /// A node to represent the null literal.
    /// </summary>
    internal class NullNode : NodeBase, ILiteralNode
    {
        #region Resolve

        protected override Type ResolveInternal(Context ctx, bool mustReturn)
        {
            return typeof(NullType);
        }

        #endregion

        #region Emit

        protected override void EmitInternal(Context ctx, bool mustReturn)
        {
            var gen = ctx.CurrentMethod.Generator;
            gen.EmitNull();
        }

        #endregion

        #region Literal type

        public Type LiteralType => typeof(NullType);

        #endregion

        #region Debug

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType();
        }

        public override int GetHashCode()
        {
            return 0;
        }

        public override string ToString()
        {
            return "(null)";
        }

        #endregion
    }
}