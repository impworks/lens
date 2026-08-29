using System.Collections.Generic;
using Lens.Compiler;
using Lens.Resolver;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.Internals
{
    /// <summary>
    /// Opens an awaiter over an awaited expression, and is where an expression that cannot be
    /// awaited at all is reported.
    ///
    /// The rewrite that consumes an await turns it into a handful of calls the script never
    /// contained, and the first of them asks the expression for its awaiter. Letting that call
    /// report its own failure describes the mistake as a missing method of a name the user never
    /// wrote, at a place they never wrote it. This node asks the question the source actually
    /// asked - can this be awaited - and answers it against the expression itself.
    /// </summary>
    internal class GetAwaiterNode : NodeBase
    {
        #region Constructor

        public GetAwaiterNode(NodeBase expression)
        {
            Expression = expression;
        }

        #endregion

        #region Fields

        /// <summary>
        /// The expression being awaited.
        /// </summary>
        public readonly NodeBase Expression;

        #endregion

        #region Resolve

        protected override TypeEntry ResolveInternal(Context ctx, bool mustReturn)
        {
            var type = Expression.Resolve(ctx);

            // an awaitable is matched structurally rather than against Task, and an extension
            // method is as good a GetAwaiter as a declared one - the call that follows would have
            // found either
            var awaiter = FindAwaiter(ctx, type);
            if (awaiter == null)
                Error(Expression, CompilerMessages.NotAwaitable, type);

            return awaiter.ReturnType;
        }

        /// <summary>
        /// Resolves the parameterless GetAwaiter of a type, or null if it has none.
        /// </summary>
        private static MethodWrapper FindAwaiter(Context ctx, TypeEntry type)
        {
            try
            {
                return ctx.ResolveMethod(type, "GetAwaiter", new TypeEntry[0]);
            }
            catch (KeyNotFoundException)
            {
            }

            if (!ctx.Options.AllowExtensionMethods)
                return null;

            try
            {
                return ctx.ResolveExtensionMethod(type, "GetAwaiter", new TypeEntry[0]);
            }
            catch (KeyNotFoundException)
            {
                return null;
            }
        }

        #endregion

        #region Transform

        internal override IEnumerable<NodeChild> GetChildren()
        {
            yield return new NodeChild(Expression);
        }

        protected override NodeBase Expand(Context ctx, bool mustReturn)
        {
            return Expr.Invoke(Expression, "GetAwaiter");
        }

        #endregion

        #region Debug

        public override string ToString()
        {
            return $"awaiter({Expression})";
        }

        #endregion
    }
}
