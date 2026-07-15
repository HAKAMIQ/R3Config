using R3Config.Mdl;

namespace R3Config.Svc;

public sealed class StateSvc
{
    private static readonly string[] LowKeys =
    [
        "resolution scale",
        "resolution scale threshold",
        "default resolution",
        "anisotropic filter",
        "anti-aliasing",
        "shader quality",
        "asynchronous texture streaming",
        "multithreaded rsx"
    ];

    public RecSum Evaluate(
        Rpcs3GameDescriptor game,
        IReadOnlyList<WikiRec> recs,
        Func<string, string, string?> readCurrent,
        HwInfo? hw = null)
    {
        var changes = recs
            .Where(rec => string.Equals(rec.Serial, game.Serial, StringComparison.OrdinalIgnoreCase))
            .SelectMany(rec => rec.Changes)
            .ToArray();

        var items = changes
            .Select(change => Check(game.Serial, change, readCurrent, hw))
            .ToArray();

        return new RecSum(
            Serial: game.Serial,
            Total: items.Length,
            Applied: items.Count(item => item.State == Mdl.RecState.Applied),
            Pending: items.Count(item => item.State == Mdl.RecState.Pending),
            Manual: items.Count(item => item.State == Mdl.RecState.Manual),
            Blocked: items.Count(item => item.State == Mdl.RecState.Blocked),
            Unknown: items.Count(item => item.State == Mdl.RecState.Unknown),
            Items: items);
    }

    private static RecItem Check(
        string serial,
        CfgChg change,
        Func<string, string, string?> readCurrent,
        HwInfo? hw)
    {
        string? current;
        try
        {
            current = readCurrent(change.Section, change.Key);
        }
        catch
        {
            return NewItem(serial, change, null, Mdl.RecState.Unknown, isDifferent: true, isActionable: false, isAutoSelectable: false);
        }

        if (CfgNorm.Same(current, change.Value))
        {
            return NewItem(serial, change, current, Mdl.RecState.Applied, isDifferent: false, isActionable: false, isAutoSelectable: false);
        }

        if (Is(change, hw))
        {
            return NewItem(serial, change, current, Mdl.RecState.Blocked, isDifferent: true, isActionable: false, isAutoSelectable: false);
        }

        if (IsAuto(change.Risk))
        {
            return NewItem(serial, change, current, Mdl.RecState.Pending, isDifferent: true, isActionable: true, isAutoSelectable: true);
        }

        return NewItem(serial, change, current, Mdl.RecState.Manual, isDifferent: true, isActionable: true, isAutoSelectable: false);
    }

    private static RecItem NewItem(
        string serial,
        CfgChg change,
        string? current,
        Mdl.RecState state,
        bool isDifferent,
        bool isActionable,
        bool isAutoSelectable)
    {
        return new RecItem(
            Serial: serial,
            Path: change.Path,
            Current: current,
            Proposed: change.Value,
            Risk: change.Risk,
            State: state,
            IsDiff: isDifferent,
            IsAction: isActionable,
            IsSel: isAutoSelectable,
            Notes: change.Notes ?? string.Empty);
    }

    private static bool IsAuto(Mdl.RecRisk risk)
    {
        return risk is Mdl.RecRisk.Required or Mdl.RecRisk.Rec;
    }

    private static bool Is(CfgChg change, HwInfo? hw)
    {
        if (hw?.Tier != HwTier.Low)
        {
            return false;
        }

        if (change.Risk is Mdl.RecRisk.Dep or Mdl.RecRisk.PerfCost)
        {
            return true;
        }

        return LowKeys.Any(key =>
            change.WikiKey.Contains(key, StringComparison.OrdinalIgnoreCase) ||
            change.Key.Contains(key, StringComparison.OrdinalIgnoreCase));
    }
}
