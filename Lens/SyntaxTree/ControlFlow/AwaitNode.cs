using System.Collections.Generic;
using Lens.Compiler;
using Lens.Resolver;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.ControlFlow
{
    /// <summary>
    /// Suspends the function until the awaited operation finishes, and produces its result.
    ///
    /// The node never reaches emission: a function that contains one is rewritten into a state
    /// machine, and the rewrite consumes the awaits. Resolving one therefore means it appeared
    /// somewhere no state machine could consume it.
    /// </summary>
    internal class AwaitNode : NodeBase
    {
        #region Fields

        /// <summary>
        /// The operation to wait for. Anything with the awaiter shape will do - the pattern is
        /// matched structurally rather than against Task in particular.
        /// </summary>
        public NodeBase Expression { get; set; }

        #endregion

        #region Resolve

        protected override TypeEntry ResolveInternal(Context ctx, bool mustReturn)
        {
            Error(CompilerMessages.AwaitPosition);
            return TypeEntryCache.Of<UnitType>();
        }

        #endregion

        #region Transform

        internal override IEnumerable<NodeChild> GetChildren()
        {
            yield return new NodeChild(Expression);
        }

        #endregion

        #region Debug

        protected bool Equals(AwaitNode other)
        {
            return Equals(Expression, other.Expression);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((AwaitNode) obj);
        }

        public override int GetHashCode()
        {
            return Expression != null ? Expression.GetHashCode() : 0;
        }

        public override string ToString()
        {
            return $"await({Expression})";
        }

        #endregion
    }
}
