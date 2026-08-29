using JetBrains.Lifetimes;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.ExtensionsAPI.Tree;
using JetBrains.ReSharper.Psi.Parsing;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.ReSharper.Psi.TreeBuilder;
using JetBrains.Text;
using Lens.Rider.Backend.Psi.Tree;

namespace Lens.Rider.Backend.Psi.Parsing;

/// <remarks>
/// Marks the whole token stream and closes it as a LENS_FILE. TreeStructureBuilderBase would bring
/// error recovery, whitespace filtering and parser messages along, none of which a one-token
/// language has any use for.
/// </remarks>
internal class LensParser(ILexer<int> lexer) : IParser, IPsiBuilderTokenFactory
{
    public IFile ParseFile()
    {
        return Lifetime.Using(lifetime =>
        {
            var builder = new PsiBuilder(lexer, LensElementType.LENS_FILE, this, lifetime);
            var mark = builder.Mark();

            while (!builder.Eof())
                builder.AdvanceLexer();

            builder.Done(mark, LensElementType.LENS_FILE, null);

            return (IFile) builder.BuildTree();
        });
    }

    public LeafElementBase CreateToken(TokenNodeType tokenNodeType, IBuffer buffer, int startOffset, int endOffset) =>
        tokenNodeType.Create(buffer, new TreeOffset(startOffset), new TreeOffset(endOffset));
}
