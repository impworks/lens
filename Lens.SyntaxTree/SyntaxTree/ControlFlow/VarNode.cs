using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.ControlFlow
{
	/// <summary>
	/// The variable declaration node.
	/// </summary>
	public class VarNode : NameDeclarationBase
	{
		public VarNode(string name = null)
		{
			NameInfo = new LexicalNameInfo
			{
				Name = name,
				IsConstant = false
			};
		}
	}
}
