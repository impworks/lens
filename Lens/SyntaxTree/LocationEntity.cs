namespace Lens.SyntaxTree
{
	/// <summary>
	/// A base class for source code entities that have a location.
	/// </summary>
	public class LocationEntity
	{
		/// <summary>
		/// Current entity's starting position (for error reporting).
		/// </summary>
		public LexemLocation StartLocation { get; set; }

		/// <summary>
		/// Current entity's ending position (for error reporting).
		/// </summary>
		public LexemLocation EndLocation { get; set; }
	}
}
