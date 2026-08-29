using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.Resources;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.Parsing;
using JetBrains.Text;
using JetBrains.UI.Icons;
using Lens.Rider.Backend.ProjectModel;

namespace Lens.Rider.Backend.Psi;

/// <summary>
/// Ties the .lns project file type to the LENS PSI language, which is what gets a .lns file a PSI
/// source file at all.
/// </summary>
[ProjectFileType(typeof(LensProjectFileType))]
public class LensProjectFileLanguageService() : ProjectFileLanguageService(LensProjectFileType.Instance)
{
    public override ILexerFactory GetMixedLexerFactory(ISolution solution, IBuffer buffer,
        IPsiSourceFile sourceFile = null)
    {
        var languageService = LensLanguage.Instance.LanguageService();
        return languageService?.GetPrimaryLexerFactory();
    }

    protected override PsiLanguageType PsiLanguageType => (PsiLanguageType) LensLanguage.Instance ?? UnknownLanguage.Instance!;

    public override IconId Icon => ServicesNavigationThemedIcons.UsageOther.Id;

    public override IPsiSourceFileProperties GetPsiProperties(IProjectFile projectFile, IPsiSourceFile sourceFile,
        IsCompileService isCompileService)
    {
        return new LensPsiProjectFileProperties(projectFile, sourceFile);
    }
}
