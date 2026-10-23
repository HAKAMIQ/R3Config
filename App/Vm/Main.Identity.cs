using R3Config.Svc;

namespace R3Config.Vm;

// Selected-game identity header parts (Ctrl/GameIdentityTemplate.xaml): the title is
// content-directed text, serial and version are technical values, and
// SelGame stays the combined accessible name.
public sealed partial class MainVm
{
    public string SelTitle => GameList.SelGame?.Title ?? string.Empty;

    public string SelSerial => GameList.SelGame?.Serial ?? string.Empty;

    public string SelVersion => GameList.SelGame?.Game.Version is { } version &&
                                !string.IsNullOrWhiteSpace(version)
        ? $"v{PatVer.Norm(version)}"
        : string.Empty;

    public bool HasSelVersion => SelVersion.Length > 0;
}
