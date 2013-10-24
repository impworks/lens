namespace Lens.SyntaxTree.ControlFlow
{
	/// <summary>
	/// The variable declaration node.
	/// </summary>
	internal class VarNode : NameDeclarationNodeBase
	{
		public VarNode(string name = null) : base(name, false)
		{ }
	}
}
