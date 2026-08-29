using JetBrains.Annotations;
using JetBrains.ReSharper.Psi;

namespace Lens.Rider.Backend.Psi;

[LanguageDefinition(Name)]
public class LensLanguage() : KnownLanguage(Name, "LENS")
{
    private new const string Name = "LENS";

    [CanBeNull, UsedImplicitly]
    public static LensLanguage Instance { get; private set; }
}
