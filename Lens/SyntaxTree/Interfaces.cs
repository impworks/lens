namespace Lens.SyntaxTree
{
	/// <summary>
	/// Marks a node that can either return an object or it's address in memory.
	/// </summary>
	internal interface IPointerProvider
	{
		bool PointerRequired { get; set; }
	}
}
