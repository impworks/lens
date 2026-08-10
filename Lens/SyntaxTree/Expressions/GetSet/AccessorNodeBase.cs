namespace Lens.SyntaxTree.Expressions.GetSet
{
    /// <summary>
    /// Base class for any accessor nodes (by index or member name).
    /// </summary>
    internal abstract class AccessorNodeBase : NodeBase
    {
        /// <summary>
        /// Expression to access a dynamic member.
        /// </summary>
        public NodeBase Expression { get; set; }

        /// <summary>
        /// Flag indicating that the accessor was written as "?." or "?[",
        /// and therefore short-circuits the rest of the chain when the expression is null.
        /// The short-circuiting itself is performed by the enclosing NullSafeChainNode.
        /// </summary>
        public bool IsNullSafe { get; set; }
    }
}