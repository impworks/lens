using System;
using System.Collections.Generic;
using Lens.Compiler;
using Lens.Resolver;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.ControlFlow
{
    /// <summary>
    /// A node representing the exception being thrown or rethrown.
    /// </summary>
    internal class ThrowNode : NodeBase
    {
        #region Fields

        /// <summary>
        /// The exception expression to be thrown.
        /// </summary>
        public NodeBase Expression { get; set; }

        #endregion

        #region Resolve

        protected override TypeEntry ResolveInternal(Context ctx, bool mustReturn)
        {
            // what may be thrown is decided here rather than while emitting, because an editor
            // binds the tree and never emits: a check that lives in EmitInternal is one the
            // reader of a half-written script never sees
            if (Expression == null)
            {
                // 'throw' on its own rethrows what is being handled, and outside a catch clause
                // there is nothing for it to name
                if (ctx.CurrentCatchBlock == null)
                    Error(CompilerMessages.ThrowArgumentExpected);
            }
            else
            {
                var type = Expression.Resolve(ctx);
                if (!TypeEntryCache.Of<Exception>().IsExtendablyAssignableFrom(ctx.Resolver, type))
                    Error(Expression, CompilerMessages.ThrowTypeNotException, type);
            }

            return base.ResolveInternal(ctx, mustReturn);
        }

        #endregion

        #region Transform

        internal override IEnumerable<NodeChild> GetChildren()
        {
            yield return new NodeChild(Expression);
        }

        internal override IReadOnlyList<NodeBase> Operands => Expression == null ? NoOperands : new[] {Expression};

        internal override NodeBase WithOperands(IReadOnlyList<NodeBase> operands)
        {
            var copy = Copy<ThrowNode>();
            copy.Expression = operands[0];
            return copy;
        }

        #endregion

        #region Emit

        protected override void EmitInternal(Context ctx, bool mustReturn)
        {
            var gen = ctx.CurrentMethod.Generator;

            if (Expression == null)
            {
                gen.EmitRethrow();
            }
            else
            {
                Expression.Emit(ctx, true);
                gen.EmitThrow();
            }
        }

        #endregion

        #region Debug

        protected bool Equals(ThrowNode other)
        {
            return Equals(Expression, other.Expression);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ThrowNode) obj);
        }

        public override int GetHashCode()
        {
            return (Expression != null ? Expression.GetHashCode() : 0);
        }

        public override string ToString()
        {
            return string.Format("throw({0})", Expression);
        }

        #endregion
    }
}