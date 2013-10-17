namespace Lens.SyntaxTree.Expressions
{
	/// <summary>
	/// Base class for any accessor nodes (by index or member name).
	/// </summary>
	public abstract class AccessorNodeBase : NodeBase, IStartLocationTrackingEntity
	{
		/// <summary>
		/// Expression to access a dynamic member.
		/// </summary>
		public NodeBase Expression { get; set; }
	}
}
