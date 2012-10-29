namespace Lens.SyntaxTree.SyntaxTree.ControlFlow
{
	/// <summary>
	/// The constant declaration node.
	/// </summary>
	public class LetNode : NameDeclarationBase
	{
		public LetNode()
		{
			NameInfo.IsConstant = true;
		}
	}
}
