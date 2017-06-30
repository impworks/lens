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
        /// Flag indicating that null check must be performed before accessing the member.
        /// </summary>
        public bool IsSafeNavigation { get; set; }
    }
}