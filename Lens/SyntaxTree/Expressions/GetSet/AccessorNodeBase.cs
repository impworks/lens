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
    }
}