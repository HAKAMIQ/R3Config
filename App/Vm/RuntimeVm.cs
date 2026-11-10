using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Vm;

#pragma warning disable CA1822 // Public properties are WPF instance-binding contracts.

// One fact of the Runtime Environment & Compatibility screen. The technical
// value (name, path, build, hash) is raw text shown left-to-right and never
// translated; the descriptive Code text is localized. A fact whose evidence is
// fully Known shows no state badge, every weaker state does.
public sealed class RuntimeFactVm
{
    public RuntimeFactVm(RuntimeFact fact)
    {
        Fact = fact;
    }

    public RuntimeFact Fact { get; }

    public string Id => Fact.Key;

    public string Label => UiText.GetText($"Runtime.Fact.{Fact.Key}");

    public bool IsTechnical => !string.IsNullOrWhiteSpace(Fact.Value);

    // Technical value, or the localized description when there is none.
    public string Value => IsTechnical
        ? Fact.Value!
        : Fact.Code is null
            ? UiText.GetText("Runtime.Code.NoValue")
            : UiText.GetText($"Runtime.Code.{Fact.Code}");

    public bool IsLocalizedValue => !IsTechnical;

    // The explanation that accompanies a technical value (why it is partial,
    // that it is the tested-not-required build, ...).
    public string Note => IsTechnical && Fact.Code is not null
        ? UiText.GetText($"Runtime.Code.{Fact.Code}")
        : string.Empty;

    public bool HasNote => Note.Length > 0;

    public string StateText => Fact.State == EvidenceState.Known
        ? string.Empty
        : UiText.GetText($"Runtime.State.{Fact.State}");

    public bool HasState => StateText.Length > 0;

    public EvidenceState State => Fact.State;
}

public sealed class RuntimeGroupVm
{
    // Detail facts stay behind the group's Details expander; the summary rows
    // answer the questions the screen exists for.
    private static readonly HashSet<string> DetailKeys = new(StringComparer.Ordinal)
    {
        "CpuVendor", "CpuThreads", "OsArchitecture",
        "CpuArchitecture", "CpuPhysicalCores", "CpuSmt", "CpuHybrid", "CpuNativeThreads", "CpuFeatures", "Rpcs3SchedulerLayout",
        "BuildArchitectureMatch", "Rpcs3SpuDecoders", "Rpcs3SpuDecodersLlvm", "Rpcs3AccurateDfmaUi",
        "DataBesideExecutable", "PatchesFolder", "PatchConfig",
        "BuildVersion", "BuildNumber", "BuildCommit", "BuildBranch", "BuildStage", "BuildArchitecture",
        "GameCategory"
    };

    // The copyable PPU hash rows below replace the plain joined fact.
    private const string PpuHashKey = "GamePpuHash";

    public RuntimeGroupVm(
        RuntimeFactGroup group,
        IReadOnlyList<RuntimeFact> facts,
        IReadOnlyList<RuntimePpuHash>? ppuHashes = null,
        bool gameSelected = false)
    {
        Group = group;
        var rows = facts
            .Where(fact => fact.Key != PpuHashKey)
            .Select(fact => new RuntimeFactVm(fact))
            .ToArray();
        SummaryRows = [.. rows.Where(row => !DetailKeys.Contains(row.Id))];
        DetailRows = [.. rows.Where(row => DetailKeys.Contains(row.Id))];
        var isGame = group == RuntimeFactGroup.Game;
        PpuHashes = isGame && ppuHashes is not null
            ? [.. ppuHashes.Select(hash => new RuntimePpuHashVm(hash))]
            : [];
        HasPpuHashGuidance = isGame && gameSelected && PpuHashes.Count == 0;
    }

    public RuntimeFactGroup Group { get; }

    public string Id => Group.ToString();

    public string Title => UiText.GetText($"Runtime.Group.{Group}");

    public IReadOnlyList<RuntimeFactVm> SummaryRows { get; }

    public IReadOnlyList<RuntimeFactVm> DetailRows { get; }

    public bool HasDetails => DetailRows.Count > 0;

    public string DetailsLabel => UiText.GetText("Ui.Details");

    // Game group only: one row per trusted PPU executable hash (none is
    // picked), or the HashUnknown guidance when a game is selected without one.
    public IReadOnlyList<RuntimePpuHashVm> PpuHashes { get; }

    public bool HasPpuHashes => PpuHashes.Count > 0;

    public string PpuHashLabel => UiText.GetText("Runtime.PpuHash.Label");

    public bool HasPpuHashGuidance { get; }

    public string PpuHashGuidance => HasPpuHashGuidance
        ? UiText.GetText("Setup.Warning.HashUnknown")
        : string.Empty;
}

// One trusted PPU executable hash: the raw 40-hex value (LTR, never
// translated), its localized source, the evidence time and a Copy action.
public sealed class RuntimePpuHashVm
{
    public RuntimePpuHashVm(RuntimePpuHash hash)
    {
        Item = hash;
        CopyCmd = new RelayCmd(() => RuntimeVm.CopyText(hash.Hash));
    }

    public RuntimePpuHash Item { get; }

    public string Label => UiText.GetText("Runtime.PpuHash.Label");

    public string Hash => Item.Hash;

    public string SourceText => UiText.GetText($"Runtime.PpuHash.Source.{Item.Source}");

    public bool HasTime => Item.TimeUtc is not null;

    public string TimeText => Item.TimeUtc is { } time
        ? UiDate.Text(new DateTimeOffset(
            time.Kind == DateTimeKind.Local ? time.ToUniversalTime() : DateTime.SpecifyKind(time, DateTimeKind.Utc)))
        : string.Empty;

    public string CopyLabel => UiText.GetText("Link.Copy");

    public ICommand CopyCmd { get; }
}

// Domain 5. It presents the Runtime Context snapshot and nothing else: it
// scans nothing and owns no policy, so the other four domains keep theirs.
public sealed class RuntimeVm : VmBase
{
    private RuntimeContextSnapshot _snapshot = RuntimeContextSnapshot.Empty;
    private IReadOnlyList<RuntimeGroupVm> _groups = [];

    public RuntimeVm(LogDiagVm? logDiagnostics = null)
    {
        LogDiag = logDiagnostics ?? new LogDiagVm();
    }

    // Replaceable for tests only, so no test touches the real clipboard; a
    // busy clipboard copies nothing instead of crashing.
    public static Action<string> CopyText { get; set; } = text => UiDlg.TryCopyText(text);

    // Log Diagnostics is part of this domain, not a sixth one.
    public LogDiagVm LogDiag { get; }

    public string TabOverview => UiText.GetText("Runtime.Tab.Overview");

    public string TabHardware => UiText.GetText("Runtime.Tab.Hardware");

    public string TabLogs => UiText.GetText("Runtime.Tab.Logs");

    public string TabEvidence => UiText.GetText("Runtime.Tab.Evidence");

    public RuntimeContextSnapshot Snapshot => _snapshot;

    public IReadOnlyList<RuntimeGroupVm> Groups
    {
        get => _groups;
        private set => Set(ref _groups, value);
    }

    public bool HasGroups => _groups.Count > 0;

    public string EmptyText => UiText.GetText("Runtime.Empty");

    public void Load(RuntimeContextSnapshot? snapshot)
    {
        _snapshot = snapshot ?? RuntimeContextSnapshot.Empty;
        Groups = [.. Enum.GetValues<RuntimeFactGroup>()
            .Select(group => new RuntimeGroupVm(
                group,
                _snapshot.InGroup(group),
                _snapshot.PpuHashes,
                _snapshot.GameIdentityState != EvidenceState.NotApplicable))
            .Where(group => group.SummaryRows.Count + group.DetailRows.Count > 0)];
        OnChange(nameof(HasGroups));
        OnChange(nameof(EmptyText));
    }

    // Rebuild so every localized label follows the language toggle.
    public void SetLang()
    {
        Load(_snapshot);
        LogDiag.SetLang();
        OnChange(nameof(TabOverview));
        OnChange(nameof(TabHardware));
        OnChange(nameof(TabLogs));
        OnChange(nameof(TabEvidence));
    }
}
