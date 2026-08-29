using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.ExtensionsAPI.Tree;

namespace Lens.Rider.Backend.Psi.Tree;

public class LensFile : FileElementBase
{
    public override NodeType NodeType => LensElementType.LENS_FILE;

    public override PsiLanguageType Language => LensLanguage.Instance!;
}
