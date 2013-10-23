namespace Lens.SyntaxTree.Expressions
{
	/// <summary>
	/// Base class for any accessor nodes (by index or member name).
	/// </summary>
	internal abstract class AccessorNodeBase : NodeBase, IStartLocationTrackingEntity
	{
		/// <summary>
		/// Expression to access a dynamic member.
		/// </summary>
		public NodeBase Expression { get; set; }
	}
}
