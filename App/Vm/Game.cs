using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Vm;

public sealed class GameVm : VmBase
{
    private RecSum _recStat;

    public GameVm(
        Rpcs3GameDescriptor game,
        WikiRec? recommendation,
        RecSum recState,
        string serial,
        string title,
        bool isDetectedInRpcs3,
        Rpcs3CompatibilityEntry? compatibility = null,
        RecSyncApiGame? config = null)
    {
        Game = game;
        Rec = recommendation;
        _recStat = recState;
        Serial = serial;
        Title = title;
        InR3 = isDetectedInRpcs3;
        Compatibility = compatibility;
        Config = config;
    }

    public static string ResolveTitle(
        Rpcs3GameDescriptor game,
        WikiRec? recommendation,
        Rpcs3CompatibilityEntry? compatibility = null)
    {
        ArgumentNullException.ThrowIfNull(game);

        var scannedTitle = game.Title.Trim();

        if (!string.IsNullOrWhiteSpace(
                scannedTitle) &&
            !scannedTitle.Equals(
                game.Serial,
                StringComparison.OrdinalIgnoreCase))
        {
            return scannedTitle;
        }

        var catalogTitle = recommendation?.WikiName.Trim();

        if (string.IsNullOrWhiteSpace(catalogTitle))
        {
            catalogTitle = compatibility?.Title;
        }

        return string.IsNullOrWhiteSpace(
                catalogTitle)
            ? game.Serial
            : catalogTitle;
    }

    public Rpcs3GameDescriptor Game { get; }

    public WikiRec? Rec { get; }

    public RecSum RecState
    {
        get => _recStat;
        set
        {
            if (Set(ref _recStat, value))
            {
                OnChange(nameof(Pending));
            }
        }
    }

    public string Serial { get; }

    public string Title { get; }

    public bool InR3 { get; }

    public Rpcs3CompatibilityEntry? Compatibility { get; }

    public RecSyncApiGame? Config { get; }

    public string CompatibilityLine =>
        Compatibility is null
            ? string.Empty
            : UiText.Fmt(
                "Games.CompatibilityLine",
                Compatibility.Status,
                Compatibility.Date.ToString(
                    "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture));

    // The official RPCS3 statuses (rpcs3.net/compatibility). The game row
    // shows such a status by its name in its own color, without the
    // "Compatibility:" prefix; CompatibilityLine stays its automation name.
    // Any other value keeps CompatibilityLine as the visible text.
    public bool HasOfficialStatus =>
        Compatibility is not null &&
        OfficialStatuses.Contains(Compatibility.Status, StringComparer.Ordinal);

    public string CompatibilityStatus =>
        HasOfficialStatus ? Compatibility!.Status : string.Empty;

    public string CompatibilityDateSuffix =>
        HasOfficialStatus
            ? string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $" ({Compatibility!.Date:yyyy-MM-dd})")
            : string.Empty;

    public string OtherCompatibilityLine =>
        HasOfficialStatus ? string.Empty : CompatibilityLine;

    private static readonly string[] OfficialStatuses =
        ["Playable", "Ingame", "Intro", "Loadable", "Nothing"];

    public int Pending => RecState.Pending;
}
