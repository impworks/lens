namespace Lens.SyntaxTree.SyntaxTree.Expressions
{
	/// <summary>
	/// The base node for accessing array-like structures by index.
	/// </summary>
	abstract public class IndexNodeBase : NodeBase
	{
		/// <summary>
		/// Expression to index.
		/// </summary>
		public NodeBase Expression { get; set; }

		/// <summary>
		/// The index value.
		/// </summary>
		public NodeBase Index { get; set; }
	}
}
