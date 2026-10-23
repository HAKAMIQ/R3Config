using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using R3Config.Mdl;

#pragma warning disable IDE0130

namespace R3Config.Svc;

// A knowledge file that fails validation is rejected whole: R3Config never uses part of it.
public sealed class Rpcs3KnowledgeRejectedException(string message, Exception? inner = null)
    : Exception(message, inner);

// Loads and serves the compact, generated knowledge file (Data/rpcs3-knowledge.json). It is optional
// data: a missing file means no knowledge, a rejected file means no knowledge and a stated reason.
// Lookups are exact (ordinal) on the Wiki page title the settings came from; nothing is matched by
// similarity, and every answer is an Inform-level decision built through EvidencePolicy.
public sealed class Rpcs3KnowledgeCatalog
{
    public const string FileName = "rpcs3-knowledge.json";
    public const int SchemaVersion = 1;
    public const int MaxBytes = 2 * 1024 * 1024;

    private const int MaxText = 512;
    private const string RemovalLifecycleEvidence = "LAST_TOUCH_IS_REMOVAL_COMMIT";

    // Only reasons that carry information are shipped. Bulk cleanups and unclassified removals are
    // noise, and VerifiedFixedUpstream needs independent RPCS3 evidence this file cannot hold.
    private static readonly HashSet<RemovalReason> ShippedReasons =
    [
        RemovalReason.ClaimedFixed,
        RemovalReason.SettingRemoved,
        RemovalReason.Replaced
    ];

    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public static Rpcs3KnowledgeCatalog Empty { get; } = new(new Rpcs3KnowledgeDocument(SchemaVersion, "empty", [], [], [], [], []));

    private readonly ILookup<string, HistoricalRemovalFact> _removals;
    private readonly IReadOnlyDictionary<string, SettingLifecycleFact> _lifecycles;
    private readonly ILookup<string, ThreadScalingFact> _scaling;

    public Rpcs3KnowledgeCatalog(Rpcs3KnowledgeDocument document)
    {
        Document = document;
        _removals = document.HistoricalRemovals.ToLookup(fact => fact.Page, StringComparer.Ordinal);
        _lifecycles = document.Settings
            .Where(fact => !fact.DeclaredAtFrozenHead &&
                           fact.LastChangeBuild is not null &&
                           fact.LastChangeCommit is not null &&
                           string.Equals(fact.Evidence, RemovalLifecycleEvidence, StringComparison.Ordinal))
            .ToDictionary(fact => fact.WikiKey, StringComparer.Ordinal);
        _scaling = document.ThreadScaling
            .Where(fact => fact.WikiPage is not null)
            .ToLookup(fact => fact.WikiPage!, StringComparer.Ordinal);
    }

    public Rpcs3KnowledgeDocument Document { get; }

    public bool IsEmpty => Document.Sources.Count == 0;

    public static Rpcs3KnowledgeCatalog Load(string path)
    {
        if (new FileInfo(path).Length > MaxBytes)
        {
            throw new Rpcs3KnowledgeRejectedException("Knowledge file is larger than the allowed size.");
        }

        return Parse(File.ReadAllBytes(path));
    }

    public static Rpcs3KnowledgeCatalog Parse(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length > MaxBytes)
        {
            throw new Rpcs3KnowledgeRejectedException("Knowledge file is larger than the allowed size.");
        }

        Rpcs3KnowledgeDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<Rpcs3KnowledgeDocument>(bytes, Options);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or ArgumentException)
        {
            throw new Rpcs3KnowledgeRejectedException("Knowledge file is not valid JSON.", ex);
        }

        if (document is null)
        {
            throw new Rpcs3KnowledgeRejectedException("Knowledge file is empty.");
        }

        Validate(document);
        return new Rpcs3KnowledgeCatalog(document);
    }

    // Deterministic text: fixed key order, LF line endings, a trailing newline.
    public static string Serialize(Rpcs3KnowledgeDocument document)
    {
        Validate(document);
        return JsonSerializer.Serialize(document, Options).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }

    public GameKnowledge ForPage(string? wikiPage)
    {
        if (string.IsNullOrWhiteSpace(wikiPage) || IsEmpty)
        {
            return GameKnowledge.Empty;
        }

        var removals = _removals[wikiPage]
            .Select(fact => new KnowledgeItem<HistoricalRemovalFact>(
                fact,
                Inform(EvidenceKind.HistoricalWiki, EvidenceAuthority.Wiki, isHistorical: true)))
            .ToArray();
        var lifecycles = removals
            .Select(item => item.Fact.WikiKey)
            .Distinct(StringComparer.Ordinal)
            .Where(_lifecycles.ContainsKey)
            .Select(key => _lifecycles[key])
            .OrderBy(fact => fact.WikiKey, StringComparer.Ordinal)
            .Select(fact => new KnowledgeItem<SettingLifecycleFact>(
                fact,
                Inform(EvidenceKind.Rpcs3Lifecycle, EvidenceAuthority.Official, isHistorical: true,
                    "WikiPage+WikiKey:Match")))
            .ToArray();
        var scaling = _scaling[wikiPage]
            .Select(fact => new KnowledgeItem<ThreadScalingFact>(
                fact,
                Inform(EvidenceKind.HardwareData, EvidenceAuthority.Community, isHistorical: false)))
            .ToArray();

        return removals.Length == 0 && lifecycles.Length == 0 && scaling.Length == 0
            ? GameKnowledge.Empty
            : new GameKnowledge(removals, lifecycles, scaling);
    }

    public GameKnowledge ForGame(string serial, WikiRec? recommendation)
    {
        if (string.IsNullOrWhiteSpace(serial) ||
            recommendation is null ||
            recommendation.Binding != WikiGameIdRole.Primary ||
            !string.Equals(recommendation.Serial, serial, StringComparison.OrdinalIgnoreCase))
        {
            return GameKnowledge.Empty;
        }

        return ForPage(recommendation.WikiName);
    }

    // Exact CPU name only. Keep the workbook's list and operating context: Stock and
    // Overclocked are separate observations, never an assertion about the host's clock.
    public IReadOnlyList<KnowledgeItem<CpuTierFact>> ForCpu(string? cpuName)
    {
        if (string.IsNullOrWhiteSpace(cpuName) || IsEmpty) return [];
        var facts = Document.CpuTiers.Where(fact => string.Equals(fact.Cpu, cpuName, StringComparison.Ordinal))
            .Distinct().OrderBy(fact => fact.List, StringComparer.Ordinal)
            .ThenBy(fact => fact.Block, StringComparer.Ordinal).ToArray();
        if (facts.GroupBy(fact => (fact.List, fact.Block)).Any(group => group.Count() > 1)) return [];
        return facts.Select(fact => new KnowledgeItem<CpuTierFact>(fact,
            Inform(EvidenceKind.HardwareData, EvidenceAuthority.Community, false, "CpuName:Match"))).ToArray();
    }

    // The identity of a page-keyed fact is the exact Wiki page the game's settings came from, so the
    // applicability is Exact; what R3Config may do with it is still decided by the policy. Knowledge is
    // display evidence: Suggest is the ceiling, and it is never Apply.
    private static SettingDecision Inform(EvidenceKind kind, EvidenceAuthority authority, bool isHistorical,
        string identityReason = "WikiPage:Match")
    {
        var assessment = new EvidenceAssessment(ApplicabilityLevel.Exact, false, [identityReason]);
        var action = EvidencePolicy.Decide(
            assessment,
            authority,
            isHistorical,
            buildApplicabilityProven: false,
            deterministicValue: false);
        if (action == SettingAction.Apply)
        {
            action = SettingAction.Suggest;
        }

        return new SettingDecision(action, assessment.Applicability, kind, authority, assessment.Reasons);
    }

    public static void Validate(Rpcs3KnowledgeDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        static void Fail(string message) => throw new Rpcs3KnowledgeRejectedException(message);

        static void Text(string? value, string what, bool required = true, int max = MaxText)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                if (required) Fail($"{what} is required.");
                return;
            }

            if (value.Length > max || value.Any(char.IsControl))
            {
                Fail($"{what} is too long or contains control characters.");
            }
        }

        static bool IsHex(string? value, int length) =>
            value is not null && value.Length == length && value.All(Uri.IsHexDigit);

        if (document.Schema != SchemaVersion) Fail("Unsupported knowledge schema.");
        Text(document.DataVersion, "DataVersion");
        if (document.Sources is null || document.Settings is null || document.HistoricalRemovals is null ||
            document.ThreadScaling is null || document.CpuTiers is null)
        {
            throw new Rpcs3KnowledgeRejectedException("Knowledge lists are required.");
        }

        if (document.Sources.Count == 0) Fail("Knowledge carries no source.");
        if (document.Sources.Any(source => source is null) ||
            document.Settings.Any(fact => fact is null) ||
            document.HistoricalRemovals.Any(fact => fact is null) ||
            document.ThreadScaling.Any(fact => fact is null) ||
            document.CpuTiers.Any(fact => fact is null))
        {
            Fail("Knowledge lists must not contain null entries.");
        }

        if (document.Sources.Select(s => s.Id).Distinct(StringComparer.Ordinal).Count() != document.Sources.Count)
        {
            Fail("Knowledge sources must have unique ids.");
        }

        foreach (var source in document.Sources)
        {
            Text(source.Id, "Source id");
            Text(source.Kind, "Source kind");
            Text(source.Name, "Source name");
            Text(source.Note, "Source note", required: false);
            if (!IsHex(source.Sha256, 64)) Fail("Source SHA-256 must be 64 hex characters.");
        }

        foreach (var fact in document.Settings)
        {
            Text(fact.WikiKey, "Setting key");
            Text(fact.DisplayName, "Setting display name");
            Text(fact.Evidence, "Setting evidence");
            if ((fact.LastChangeBuild is null) != (fact.LastChangeCommit is null) ||
                fact.LastChangeBuild is <= 0 ||
                fact.LastChangeCommit is not null && !IsHex(fact.LastChangeCommit, 40))
            {
                Fail("A setting build and commit must be both proven or both absent.");
            }
        }

        if (document.Settings.Select(f => f.WikiKey).Distinct(StringComparer.OrdinalIgnoreCase).Count() != document.Settings.Count)
        {
            Fail("Setting lifecycle keys must be unique.");
        }

        foreach (var fact in document.HistoricalRemovals)
        {
            Text(fact.Page, "Removal page");
            Text(fact.WikiKey, "Removal key");
            Text(fact.Basis, "Removal basis");
            Text(fact.ObservedUntil, "Removal observed-until");
            Text(fact.EditComment, "Removal comment", required: false, max: 300);
            if (!ShippedReasons.Contains(fact.Reason)) Fail("A removal reason that carries no information must not ship.");
        }

        if (document.HistoricalRemovals.Select(f => (f.Page, f.WikiKey, f.ObservedUntil)).Distinct().Count() != document.HistoricalRemovals.Count)
        {
            Fail("Historical removals must be unique.");
        }

        foreach (var fact in document.ThreadScaling)
        {
            Text(fact.Game, "Thread-scaling game");
            Text(fact.WikiPage, "Thread-scaling page", required: false);
            if (fact.WikiPage is not null && !string.Equals(fact.Game, fact.WikiPage, StringComparison.Ordinal))
            {
                Fail("A thread-scaling page must exactly equal its source game name.");
            }
            Text(fact.CoreThreadConfig, "Thread-scaling configuration");
            Text(fact.Grade, "Thread-scaling grade");
            Text(fact.Note, "Thread-scaling note", required: false, max: 300);
            Text(fact.FpsTarget, "Thread-scaling FPS target", required: false, max: 32);
            if (fact.Cores is <= 0 || fact.Threads is <= 0)
            {
                Fail("Thread-scaling numbers must be positive.");
            }

            if (fact.SourceLink is not null &&
                !(Uri.TryCreate(fact.SourceLink, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"))
            {
                Fail("A thread-scaling link must be http(s).");
            }
        }

        // The workbook grade is community display text, never the host hardware
        // tier; deconstruction keeps this validator outside the hardware-tier
        // reader gate (EvidenceApplicabilityT) without widening its allow-list.
        foreach (var (list, block, grade, cpu) in document.CpuTiers)
        {
            Text(list, "Tier list");
            Text(block, "Tier block");
            Text(grade, "Tier level");
            Text(cpu, "Tier CPU");
        }
    }
}

#pragma warning restore IDE0130
