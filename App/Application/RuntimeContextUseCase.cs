using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Application;

// Read-only composition of the Runtime Context snapshot (Domain 5). It only
// combines evidence the scanners already collected; it scans nothing, writes
// nothing and owns no policy of Game Settings, Patches, Canary or Artemis.
public sealed class RuntimeContextUseCase(string hostPlatform = Rpcs3BuildCatalog.WindowsPlatform)
{
    public RuntimeContextSnapshot Build(
        HwInfo? hardware,
        Rpcs3InstallDescriptor? install,
        Rpcs3BuildIdentity? build,
        Rpcs3GameDescriptor? game,
        GameSettingsModel? settings,
        Rpcs3BuildCatalog? builds,
        DiagnosticEvidenceCatalog? evidence = null)
    {
        return RuntimeContextBuilder.Build(
            hardware,
            install,
            build,
            game,
            settings,
            builds,
            hostPlatform,
            evidence);
    }
}
