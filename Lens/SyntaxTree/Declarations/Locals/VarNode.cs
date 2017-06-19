namespace Lens.SyntaxTree.Declarations.Locals
{
    /// <summary>
    /// The variable declaration node.
    /// </summary>
    internal class VarNode : NameDeclarationNodeBase
    {
        public VarNode(string name = null) : base(name, false)
        {
        }
    }
}