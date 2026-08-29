using JetBrains.Application.BuildScript.Application.Zones;

namespace Lens.Rider.Backend;

[ZoneMarker]
public class ZoneMarker : IRequire<ILanguageLensZone>;
