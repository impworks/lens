namespace Lens.SyntaxTree
{
	/// <summary>
	/// A marker inteface indicating the node requires a start location.
	/// </summary>
	public interface IStartLocationTrackingEntity {}

	/// <summary>
	/// A marker interface indicating the node requires an end location.
	/// </summary>
	public interface IEndLocationTrackingEntity {}

	/// <summary>
	/// Marks a node that can either return an object or it's address in memory.
	/// </summary>
	public interface IPointerProvider
	{
		bool PointerRequired { get; set; }
	}
}
