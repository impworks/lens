namespace Lens.SyntaxTree.SyntaxTree.ControlFlow
{
	/// <summary>
	/// The constant declaration node.
	/// </summary>
	public class LetNode : NameDeclarationBase
	{
		public LetNode(string name = null)
			: base(true)
		{
			Name = name;
		}
	}
}
