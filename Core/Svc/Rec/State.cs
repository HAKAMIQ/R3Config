using R3Config.Mdl;

namespace R3Config.Svc;

#pragma warning disable CA1822

// Decides the state of each documented change from the game, the evidence class
// and the value RPCS3 would currently use. It does not read the machine: a
// generic hardware tier never blocks, unblocks or demotes documented evidence.
// A hardware condition may only come from the evidence itself (see ChangeScope): the scope a
// change states in its own note is compared with the runtime facts through EvidencePolicy, and
// only an Exact, deterministic change is auto-selectable. A scope mismatch blocks; an unresolved
// or unprovable scope is never applied automatically.
public sealed class StateSvc
{
    public RecSum Evaluate(
        Rpcs3GameDescriptor game,
        IReadOnlyList<WikiRec> recs,
        Func<string, string, string?> readCurrent,
        EvidenceRuntimeFacts? facts = null)
    {
        facts ??= RuntimeFacts.Create(game);

        var changes = recs
            .Where(rec =>
                string.Equals(rec.Serial, game.Serial, StringComparison.OrdinalIgnoreCase) &&
                rec.Binding == WikiGameIdRole.Primary)
            .SelectMany(rec => rec.Changes)
            .ToArray();

        var items = changes
            .Select(change => Check(game.Serial, change, readCurrent, facts))
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
        EvidenceRuntimeFacts facts)
    {
        if (!IsGameConfigClass(change.Class))
        {
            return NewItem(
                serial,
                change,
                null,
                Mdl.RecState.Blocked,
                isDifferent: true,
                isActionable: false,
                isAutoSelectable: false);
        }

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

        var assessment = EvidencePolicy.Assess(ChangeScope.Conditions(serial, change.Notes), facts);
        var action = EvidencePolicy.Decide(
            assessment,
            EvidenceAuthority.Wiki,
            isHistorical: false,
            buildApplicabilityProven: true,
            deterministicValue: IsAuto(change.Risk) && change.Origin == RecOrigin.Baseline);
        var decision = new SettingDecision(
            action,
            assessment.Applicability,
            EvidenceKind.CurrentWikiConfig,
            EvidenceAuthority.Wiki,
            assessment.Reasons);

        return action switch
        {
            SettingAction.Blocked => NewItem(serial, change, current, Mdl.RecState.Blocked, isDifferent: true, isActionable: false, isAutoSelectable: false, decision),
            SettingAction.Apply => NewItem(serial, change, current, Mdl.RecState.Pending, isDifferent: true, isActionable: true, isAutoSelectable: true, decision),
            _ => NewItem(serial, change, current, Mdl.RecState.Manual, isDifferent: true, isActionable: true, isAutoSelectable: false, decision)
        };
    }

    private static RecItem NewItem(
        string serial,
        CfgChg change,
        string? current,
        Mdl.RecState state,
        bool isDifferent,
        bool isActionable,
        bool isAutoSelectable,
        SettingDecision? decision = null)
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
            Notes: change.Notes ?? string.Empty,
            Class: change.Class,
            Decision: decision);
    }

    private static bool IsGameConfigClass(WikiRecommendationClass classification)
    {
        return classification is
            WikiRecommendationClass.MandatoryGameConfig or
            WikiRecommendationClass.GeneralRecommendation;
    }

    private static bool IsAuto(Mdl.RecRisk risk)
    {
        return risk is Mdl.RecRisk.Required or Mdl.RecRisk.Rec;
    }
}

#pragma warning restore CA1822
