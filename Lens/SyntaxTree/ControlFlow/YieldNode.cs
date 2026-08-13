using System.Collections.Generic;
using Lens.Compiler;
using Lens.Resolver;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.ControlFlow
{
    /// <summary>
    /// Hands a value - or a whole sequence, for 'yield from' - to whoever is iterating the function.
    ///
    /// The node never reaches emission: a function that contains one is rewritten into a state
    /// machine, and the rewrite consumes the yields. Resolving one therefore means it appeared
    /// somewhere a state machine could not be built.
    /// </summary>
    internal class YieldNode : NodeBase
    {
        #region Fields

        /// <summary>
        /// The value to yield, or the sequence to yield the items of.
        /// </summary>
        public NodeBase Expression { get; set; }

        /// <summary>
        /// Whether the expression is a single item or a sequence of them.
        /// </summary>
        public bool IsSequence { get; set; }

        #endregion

        #region Resolve

        protected override TypeEntry ResolveInternal(Context ctx, bool mustReturn)
        {
            Error(CompilerMessages.YieldNotInIterator);
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

        protected bool Equals(YieldNode other)
        {
            return IsSequence == other.IsSequence && Equals(Expression, other.Expression);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((YieldNode) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (IsSequence.GetHashCode() * 397) ^ (Expression != null ? Expression.GetHashCode() : 0);
            }
        }

        public override string ToString()
        {
            return IsSequence ? $"yield from({Expression})" : $"yield({Expression})";
        }

        #endregion
    }
}
