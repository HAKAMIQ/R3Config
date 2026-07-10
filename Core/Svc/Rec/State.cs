using R3Config.Mdl;

namespace R3Config.Svc;

public sealed class RecStateService
{
    private static readonly string[] LowEndBlockedKeys =
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

    public RecStateSummary Evaluate(
        Rpcs3GameDescriptor game,
        IReadOnlyList<WikiGameRecommendation> recs,
        Func<string, string, string?> readCurrent,
        HwInfo? hw = null)
    {
        var changes = recs
            .Where(rec => string.Equals(rec.Serial, game.Serial, StringComparison.OrdinalIgnoreCase))
            .SelectMany(rec => rec.Changes)
            .ToArray();

        var items = changes
            .Select(change => EvaluateItem(game.Serial, change, readCurrent, hw))
            .ToArray();

        return new RecStateSummary(
            Serial: game.Serial,
            Total: items.Length,
            Applied: items.Count(item => item.State == RecState.Applied),
            Pending: items.Count(item => item.State == RecState.Pending),
            Manual: items.Count(item => item.State == RecState.Manual),
            Blocked: items.Count(item => item.State == RecState.Blocked),
            Unknown: items.Count(item => item.State == RecState.Unknown),
            Items: items);
    }

    private static RecStateItem EvaluateItem(
        string serial,
        ConfigChange change,
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
            return NewItem(serial, change, null, RecState.Unknown, isDifferent: true, isActionable: false, isAutoSelectable: false);
        }

        if (CfgValueNorm.Same(current, change.Value))
        {
            return NewItem(serial, change, current, RecState.Applied, isDifferent: false, isActionable: false, isAutoSelectable: false);
        }

        if (IsBlocked(change, hw))
        {
            return NewItem(serial, change, current, RecState.Blocked, isDifferent: true, isActionable: false, isAutoSelectable: false);
        }

        if (IsAutoSafe(change.Risk))
        {
            return NewItem(serial, change, current, RecState.Pending, isDifferent: true, isActionable: true, isAutoSelectable: true);
        }

        return NewItem(serial, change, current, RecState.Manual, isDifferent: true, isActionable: true, isAutoSelectable: false);
    }

    private static RecStateItem NewItem(
        string serial,
        ConfigChange change,
        string? current,
        RecState state,
        bool isDifferent,
        bool isActionable,
        bool isAutoSelectable)
    {
        return new RecStateItem(
            Serial: serial,
            Path: change.DisplayPath,
            Current: current,
            Proposed: change.Value,
            Risk: change.Risk,
            State: state,
            IsDifferent: isDifferent,
            IsActionable: isActionable,
            IsAutoSelectable: isAutoSelectable,
            Notes: change.Notes ?? string.Empty);
    }

    private static bool IsAutoSafe(RecommendationRisk risk)
    {
        return risk is RecommendationRisk.Required or RecommendationRisk.Recommended;
    }

    private static bool IsBlocked(ConfigChange change, HwInfo? hw)
    {
        if (hw?.Tier != HwTier.Low)
        {
            return false;
        }

        if (change.Risk is RecommendationRisk.HardwareDependent or RecommendationRisk.PerformanceCost)
        {
            return true;
        }

        return LowEndBlockedKeys.Any(key =>
            change.WikiKey.Contains(key, StringComparison.OrdinalIgnoreCase) ||
            change.Key.Contains(key, StringComparison.OrdinalIgnoreCase));
    }
}
