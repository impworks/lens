using System.Collections.Generic;
using System.Linq;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.Debugger;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.Modules;
using JetBrains.Util;
using Lens.Rider.Backend.ProjectModel;
using Lens.Rider.Backend.Psi;

namespace Lens.Rider.Backend.Debugger;

/// <summary>
/// The answer to DotNetLineBreakpointType.computeVariants: BreakpointVariantsEnumerator looks a
/// provider up by the language of the PSI file, so this is a language component rather than a
/// solution one. A LineBreakpoint becomes a line-wide variant with no highlight range, which is all
/// a one-token tree could honestly offer - TextRangeBreakpoint.Create needs a real document range.
///
/// Whether a line is blank, a comment or a statement is not decided here: canPutAt on the frontend
/// and lens-language-server own that question.
/// </summary>
[Language(typeof(LensLanguage))]
public class LensBreakpointVariantsProvider : IBreakpointVariantsProvider
{
    public IReadOnlyList<IBreakpoint> GetBreakpointVariants(IProjectFile file, int line, ISolution solution)
    {
        var variants = new List<IBreakpoint>();

        var sourceFile = solution.PsiModules().GetPsiSourceFilesFor(file).FirstOrDefault();
        if (sourceFile == null)
            return null;

        var document = sourceFile.Document;

        var lineCount = (int)document.GetLineCount();
        if (line > lineCount)
            return variants;

        variants.Add(new LineBreakpoint());
        return variants;
    }

    public IEnumerable<string> GetSupportedFileExtensions()
    {
        return LensProjectFileType.Instance?.Extensions.ToList() ?? EmptyList<string>.Enumerable;
    }
}
