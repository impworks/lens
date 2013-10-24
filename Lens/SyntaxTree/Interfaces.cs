namespace Lens.SyntaxTree
{
	/// <summary>
	/// A marker inteface indicating the node requires a start location.
	/// </summary>
	internal interface IStartLocationTrackingEntity { }

	/// <summary>
	/// A marker interface indicating the node requires an end location.
	/// </summary>
	internal interface IEndLocationTrackingEntity { }

	/// <summary>
	/// Marks a node that can either return an object or it's address in memory.
	/// </summary>
	internal interface IPointerProvider
	{
		bool PointerRequired { get; set; }
	}
}
