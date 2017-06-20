namespace Lens.SyntaxTree.Declarations.Locals
{
    /// <summary>
    /// The constant declaration node.
    /// </summary>
    internal class LetNode : NameDeclarationNodeBase
    {
        public LetNode(string name = null) : base(name, true)
        {
        }
    }
}