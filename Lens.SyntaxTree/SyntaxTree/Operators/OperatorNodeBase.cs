namespace Lens.SyntaxTree.SyntaxTree.Operators
{
	/// <summary>
	/// A base node for all operators.
	/// </summary>
	public abstract class OperatorNodeBase : NodeBase
	{
		/// <summary>
		/// A textual operator representation for error reporting.
		/// </summary>
		public abstract string OperatorRepresentation { get; }
	}
}
