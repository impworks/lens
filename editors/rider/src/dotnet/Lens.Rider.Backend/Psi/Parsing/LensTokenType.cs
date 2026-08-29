using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.ExtensionsAPI.Tree;
using JetBrains.ReSharper.Psi.Parsing;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.Text;

namespace Lens.Rider.Backend.Psi.Parsing;

/// <summary>
/// One token, spanning the whole file. All language intelligence for LENS lives in
/// lens-language-server over LSP; the backend needs a PSI file only so that the breakpoint
/// pipeline can find a language to look a variants provider up by.
/// </summary>
[Language(typeof(LensLanguage))]
public class LensTokenType : INodeTypesInitializer
{
    public static readonly TokenNodeType TEXT = new LensTextTokenNodeType("TEXT", 0);

    private sealed class LensTextTokenNodeType(string s, int index) : TokenNodeType(s, index)
    {
        public override LeafElementBase Create(IBuffer buffer, TreeOffset startOffset, TreeOffset endOffset) =>
            new LensTextToken(this, buffer, startOffset, endOffset);

        public override bool IsWhitespace => false;
        public override bool IsComment => false;
        public override bool IsStringLiteral => false;
        public override bool IsConstantLiteral => false;
        public override bool IsIdentifier => false;
        public override bool IsKeyword => false;

        public override string TokenRepresentation => "TEXT";
    }

    private sealed class LensTextToken(NodeType nodeType, IBuffer buffer, TreeOffset startOffset, TreeOffset endOffset)
        : BoundToBufferLeafElement(nodeType, buffer, startOffset, endOffset), ITokenNode
    {
        public override PsiLanguageType Language => LanguageFromParent;

        public TokenNodeType GetTokenType() => (TokenNodeType) NodeType;
    }
}
