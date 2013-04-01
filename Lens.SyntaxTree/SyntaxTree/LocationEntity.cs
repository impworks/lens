using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree
{
	/// <summary>
	/// A base class for source code entities that have a location.
	/// </summary>
	public class LocationEntity
	{
		/// <summary>
		/// Current entity's starting position (for error reporting).
		/// </summary>
		public virtual LexemLocation StartLocation { get; set; }

		/// <summary>
		/// Current entity's ending position (for error reporting).
		/// </summary>
		public virtual LexemLocation EndLocation { get; set; }
	}
}
