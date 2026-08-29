using JetBrains.Application.BuildScript.Application.Zones;
using JetBrains.ReSharper.Feature.Services;
using JetBrains.ReSharper.Psi;

namespace Lens.Rider.Backend;

[ZoneDefinition]
public interface ILanguageLensZone : IPsiLanguageZone, IRequire<ICodeEditingZone>;
