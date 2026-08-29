using System.Collections.Generic;
using JetBrains.Application.Components;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.ExtensionsAPI.Caches2;
using JetBrains.ReSharper.Psi.Impl;
using JetBrains.ReSharper.Psi.Modules;
using JetBrains.ReSharper.Psi.Parsing;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.Util;
using Lens.Rider.Backend.Psi.Parsing;

namespace Lens.Rider.Backend.Psi;

/// <summary>
/// Enough of a language service to make IProjectFile.GetPrimaryPsiFile() return a file whose
/// Language is LENS - no caches, no type members, no declared elements.
/// </summary>
[Language(typeof(LensLanguage))]
public class LensLanguageService(PsiLanguageType psiLanguageType, ILazy<IConstantValueService> constantValueService)
    : LanguageService(psiLanguageType, constantValueService)
{
    public override ILexerFactory GetPrimaryLexerFactory() => new LensLexerFactory();

    public override ILexer CreateFilteringLexer(ILexer lexer) => lexer;

    public override IParser CreateParser(ILexer lexer, IPsiModule module, IPsiSourceFile sourceFile) =>
        new LensParser(lexer as ILexer<int> ?? lexer.ToCachingLexer());

    public override IEnumerable<ITypeDeclaration> FindTypeDeclarations(IFile file) =>
        EmptyList<ITypeDeclaration>.Enumerable;

    public override ILanguageCacheProvider CacheProvider => null;

    public override bool IsCaseSensitive => true;

    public override bool SupportTypeMemberCache => false;

    public override ITypePresenter TypePresenter => DefaultTypePresenter.Instance;
}
