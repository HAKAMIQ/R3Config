using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using R3Config.Mdl;

#pragma warning disable IDE0130

namespace R3Config.Svc;

// The machine/game facts a log session is compared with. Every field is null
// when the Runtime Context could not prove it, and null never matches a
// documented condition.
public sealed record DiagnosticEnvironment(
    string? Serial,
    string? AppVersion,
    IReadOnlyList<string> PpuHashes,
    int? CurrentBuild,
    string? Platform,
    string? Architecture,
    string? GpuVendor,
    string? Renderer);

// Shipped knowledge base of documented cases (Data/diagnostics-kb.json). It is
// data only: nothing in it is executed, and a missing file is the normal
// "no documented cases" state.
public sealed class DiagnosticKnowledgeBase
{
    public const int SchemaVersion = 1;
    public const string FileName = "diagnostics-kb.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public DiagnosticKnowledgeBase(IEnumerable<DiagnosticEntry> entries)
    {
        Entries = [.. entries];
    }

    public static DiagnosticKnowledgeBase Empty { get; } = new([]);

    public IReadOnlyList<DiagnosticEntry> Entries { get; }

    public static JsonSerializerOptions JsonOptions => Options;

    public static DiagnosticKnowledgeBase Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var bytes = R3Bound.ReadBytes(path, R3Limits.PatCatBy, "Diagnostic knowledge base");

        DiagnosticKnowledgeDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<DiagnosticKnowledgeDocument>(bytes, Options);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Diagnostic knowledge base JSON is invalid.", ex);
        }

        if (document is null || document.Schema != SchemaVersion || document.Entries is null || document.Entries.Any(item => item is null))
        {
            throw new InvalidDataException("Diagnostic knowledge base is empty, invalid, or an unsupported schema.");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in document.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Id) || !ids.Add(entry.Id))
            {
                throw new InvalidDataException("Diagnostic knowledge base has a missing or duplicate entry id.");
            }

            if (!WikiLinks.IsSafeUrl(entry.Source.Url) ||
                entry.Signatures.Any(signature => !signature.Id.StartsWith("s1:", StringComparison.Ordinal)) ||
                entry.Solutions.Any(solution => solution.Build is <= 0))
            {
                throw new InvalidDataException($"Diagnostic knowledge base entry {entry.Id} is invalid.");
            }
        }

        return new DiagnosticKnowledgeBase(document.Entries);
    }
}

// Decides what a log session proves. It compares only documented, exact
// conditions (title, platform, architecture, build boundaries, exact log
// signature); it never uses free-text similarity as proof, never invents a
// cause or a fix, and reports UnknownCause when nothing links the observed
// events to a documented case.
public static class DiagnosticEngine
{
    private const int MaxObserved = 20;

    public static DiagnosticReport Evaluate(
        LogSession session,
        DiagnosticEnvironment environment,
        DiagnosticKnowledgeBase knowledge)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(knowledge);

        var notes = new List<string>();
        var problems = session.Problems
            .OrderBy(item => Severity(item))
            .ThenByDescending(item => item.Count)
            .ThenBy(item => item.Line)
            .ToList();

        // The log is the evidence of the run that failed, so its own banner
        // decides the build; a different installed build is only noted.
        var build = session.Build is { BuildNumber: > 0 } banner ? banner.BuildNumber : environment.CurrentBuild;
        if (session.Build is { } logged && environment.CurrentBuild is { } installed && logged.BuildNumber != installed)
        {
            notes.Add("LogFromOtherBuild");
        }

        // A log of another game says nothing about the selected game: its
        // events stay visible, but no documented case is matched to either.
        var otherGame = session.GameSerial is not null && environment.Serial is not null &&
                        !string.Equals(session.GameSerial, environment.Serial, StringComparison.OrdinalIgnoreCase);
        if (otherGame)
        {
            notes.Add("LogFromOtherGame");
        }

        var findings = new List<DiagnosticFinding>();
        foreach (var item in problems.Take(MaxObserved))
        {
            findings.Add(new DiagnosticFinding(
                DiagnosisKind.ObservedEvent, item, null, MatchQuality.None,
                [$"category:{item.Primary}", $"count:{item.Count}"], []));
        }

        var serial = session.GameSerial ?? environment.Serial;
        foreach (var entry in otherGame ? [] : knowledge.Entries)
        {
            if (!Applies(entry, environment, serial))
            {
                continue;
            }

            var linked = problems
                .Select(item => (Item: item, Quality: Link(entry, item)))
                .Where(pair => pair.Quality != MatchQuality.None)
                .OrderByDescending(pair => pair.Quality)
                .ThenBy(pair => Severity(pair.Item))
                .FirstOrDefault();
            if (linked.Item is null)
            {
                continue;
            }

            var finding = Decide(entry, linked.Item, linked.Quality, build);
            if (finding is not null)
            {
                findings.Add(finding);
            }
        }

        if (problems.Count > 0 && !otherGame && !findings.Any(item => item.Kind != DiagnosisKind.ObservedEvent))
        {
            findings.Add(new DiagnosticFinding(
                DiagnosisKind.UnknownCause, problems[0], null, MatchQuality.None,
                ["no-documented-link"], []));
        }

        if (problems.Count == 0)
        {
            notes.Add("NoProblemEvents");
        }

        return new DiagnosticReport(session, findings, notes);
    }

    // Fatal and crash evidence first, then the rest by how much the log says.
    private static int Severity(LogEvent item) =>
        item.IsMinor ? 6 :
        item.Categories.Contains(LogCategory.Crash) ? 0 :
        item.Categories.Contains(LogCategory.FatalError) ? 1 :
        item.Categories.Contains(LogCategory.Assertion) ? 2 :
        item.Categories.Contains(LogCategory.BlackScreenIndicator) ? 3 :
        item.Level == 'E' ? 4 : 5;

    private static bool Applies(DiagnosticEntry entry, DiagnosticEnvironment environment, string? serial)
    {
        var scope = entry.Applies;
        return Match(scope.Platform, environment.Platform) &&
               Match(scope.Architecture, environment.Architecture) &&
               Match(scope.GpuVendor, environment.GpuVendor) &&
               Match(scope.Renderer, environment.Renderer) &&
               (scope.TitleIds.Count == 0 ||
                (serial is not null && scope.TitleIds.Contains(serial, StringComparer.OrdinalIgnoreCase)));

        // No documented condition never restricts; an unproven environment
        // value never satisfies a documented condition.
        static bool Match(string? wanted, string? actual) =>
            string.IsNullOrWhiteSpace(wanted) ||
            (actual is not null && string.Equals(wanted, actual, StringComparison.OrdinalIgnoreCase));
    }

    private static MatchQuality Link(DiagnosticEntry entry, LogEvent item)
    {
        if (item.Signature is { } signature &&
            entry.Signatures.Any(reference => string.Equals(reference.Id, signature.Id, StringComparison.Ordinal)))
        {
            return MatchQuality.SignatureExact;
        }

        // Scope-compatible: the documented case names this kind of problem and
        // is itself restricted to a title, platform, renderer or GPU vendor.
        var restricted = entry.Applies.TitleIds.Count > 0 || !string.IsNullOrWhiteSpace(entry.Applies.Platform) ||
                         !string.IsNullOrWhiteSpace(entry.Applies.Renderer) || !string.IsNullOrWhiteSpace(entry.Applies.GpuVendor);
        return restricted && entry.Categories.Any(category => item.Categories.Contains(category))
            ? MatchQuality.ScopeCompatible
            : MatchQuality.None;
    }

    private static DiagnosticFinding? Decide(DiagnosticEntry entry, LogEvent item, MatchQuality quality, int? build)
    {
        var reasons = new List<string> { $"quality:{quality}", $"evidence:{entry.EvidenceClass}" };
        var solutions = new List<DiagnosticSolution>();

        // Only the exact log signature establishes a known case. A link through
        // scope (title/platform plus the kind of problem) is a similar report,
        // never a cause.
        if (quality != MatchQuality.SignatureExact)
        {
            return new DiagnosticFinding(DiagnosisKind.SimilarCommunityReport, item, entry, quality, reasons, solutions);
        }

        switch (entry.Kind)
        {
            case DiagnosticEntryKind.Workaround:
                solutions.AddRange(entry.Solutions);
                return new DiagnosticFinding(DiagnosisKind.DocumentedWorkaround, item, entry, quality, reasons, solutions);

            case DiagnosticEntryKind.KnownIssue:
                solutions.AddRange(entry.Solutions);
                return new DiagnosticFinding(DiagnosisKind.KnownIssueMatch, item, entry, quality, reasons, solutions);
        }

        // Regression: the build decides.
        if (build is null || entry.FirstBrokenBuild is null)
        {
            reasons.Add("build-unproven");
            solutions.AddRange(entry.Solutions);
            return new DiagnosticFinding(DiagnosisKind.KnownIssueMatch, item, entry, quality, reasons, solutions);
        }

        if (build < entry.FirstBrokenBuild)
        {
            return null;
        }

        if (entry.FixedBuild is { } fixedBuild && build >= fixedBuild)
        {
            reasons.Add($"build:{build}>=fixed:{fixedBuild}");
            return new DiagnosticFinding(DiagnosisKind.KnownFixMatch, item, entry, quality, reasons, solutions);
        }

        reasons.Add(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"build:{build} in [{entry.FirstBrokenBuild},{(entry.FixedBuild is { } upper ? upper.ToString(System.Globalization.CultureInfo.InvariantCulture) : "open")})"));
        if (entry.FixedBuild is { } fix)
        {
            solutions.Add(new DiagnosticSolution(DiagnosticSolutionKind.FixedBuild, Build: fix));
        }
        else
        {
            solutions.Add(new DiagnosticSolution(DiagnosticSolutionKind.KnownUnresolved));
        }

        if (entry.LastGoodBuild is { } good)
        {
            solutions.Add(new DiagnosticSolution(DiagnosticSolutionKind.KnownGoodBuild, Build: good));
        }

        solutions.AddRange(entry.Solutions.Where(solution => solution.Kind is not
            (DiagnosticSolutionKind.FixedBuild or DiagnosticSolutionKind.KnownGoodBuild or DiagnosticSolutionKind.KnownUnresolved)));
        return new DiagnosticFinding(DiagnosisKind.KnownRegressionMatch, item, entry, quality, reasons, solutions);
    }
}

#pragma warning restore IDE0130
