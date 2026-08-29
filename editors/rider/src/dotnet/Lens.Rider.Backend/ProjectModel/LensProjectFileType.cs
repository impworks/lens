using JetBrains.Annotations;
using JetBrains.ProjectModel;

namespace Lens.Rider.Backend.ProjectModel;

/// <remarks>
/// Without this a .lns file is an unknown blob to the backend: it gets an IProjectFile whose
/// LanguageType is unknown, no PSI is built for it, and BreakpointVariantsEnumerator gives up.
/// </remarks>
[ProjectFileTypeDefinition(Name)]
public class LensProjectFileType() : KnownProjectFileType(Name, "LENS", [LensExtension])
{
    private new const string Name = "LENS";
    private const string LensExtension = ".lns";

    [CanBeNull, UsedImplicitly]
    public new static LensProjectFileType Instance { get; private set; }
}
