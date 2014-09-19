namespace Lens.SyntaxTree
{
	/// <summary>
	/// Marks a node that can either return an object or it's address in memory.
	/// </summary>
	internal interface IPointerProvider
	{
		/// <summary>
		/// Indicates that the pointer to the value is required.
		/// </summary>
		bool PointerRequired { get; set; }

		/// <summary>
		/// Indicates that the argument is passed by reference.
		/// </summary>
		bool RefArgumentRequired { get; set; }
	}
}
