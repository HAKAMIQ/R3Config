using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Application;

public sealed record LogDiagnosticsResult(
    DiagnosticReport Report,
    IReadOnlyList<LogSession> Sessions,
    IReadOnlyList<string> Notes,
    string? EvidenceError);

// Read-only Log Diagnostics (Domain 5). It reads the RPCS3 logs of the current
// installation, evaluates them against the shipped knowledge base and returns
// facts. It writes nothing, never touches the RPCS3 tree and never runs RPCS3.
public sealed class LogDiagnosticsUseCase(
    string hostPlatform = Rpcs3BuildCatalog.WindowsPlatform,
    string knowledgeFileName = DiagnosticKnowledgeBase.FileName)
{
    public const string InvalidKnowledgeMessage = "Diagnostic knowledge base is invalid";

    // The knowledge base is optional data: without it observed events and the
    // unknown-cause result still work, and no documented case can match.
    public DiagnosticKnowledgeBase LoadKnowledge(string baseDirectory, out string? error)
    {
        error = null;
        var path = Path.Combine(baseDirectory, "Data", knowledgeFileName);
        if (!File.Exists(path))
        {
            return DiagnosticKnowledgeBase.Empty;
        }

        try
        {
            return DiagnosticKnowledgeBase.Load(path);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            error = InvalidKnowledgeMessage;
            return DiagnosticKnowledgeBase.Empty;
        }
    }

    public LogDiagnosticsResult Analyze(
        Rpcs3InstallDescriptor install,
        string? selectedSerial,
        HwInfo? hardware,
        Rpcs3BuildIdentity? build,
        DiagnosticKnowledgeBase knowledge,
        string? knowledgeError = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(install);
        ArgumentNullException.ThrowIfNull(knowledge);

        cancellationToken.ThrowIfCancellationRequested();
        var read = Rpcs3LogService.Read(install, selectedSerial, cancellationToken);
        var notes = new List<string>(read.Notes);

        // Prefer the current log of the selected game, then any log of that
        // game, then the newest current log.
        var session =
            read.Sessions.FirstOrDefault(item => Same(item.GameSerial, selectedSerial) && item.Source?.Kind == LogSourceKind.Current) ??
            read.Sessions.FirstOrDefault(item => Same(item.GameSerial, selectedSerial)) ??
            read.Sessions.FirstOrDefault(item => item.Source?.Kind == LogSourceKind.Current) ??
            (read.Sessions.Count > 0 ? read.Sessions[0] : null);
        if (session is null)
        {
            return new LogDiagnosticsResult(DiagnosticReport.Empty, [], notes, knowledgeError);
        }

        if (selectedSerial is not null && session.GameSerial is not null && !Same(session.GameSerial, selectedSerial))
        {
            notes.Add("LogFromOtherGame");
        }

        if (session.GameSerial is null && selectedSerial is not null)
        {
            notes.Add("LogHasNoGameBoot");
        }

        if (session.Truncated)
        {
            notes.Add("Truncated");
        }
        else if (session.RunContext.Completeness == LogCompleteness.Partial)
        {
            notes.Add("PartialLog");
        }

        if (session.Analysis is not null)
        {
            // Which evidence snapshot this diagnosis was evaluated against.
            session = session with
            {
                Analysis = session.Analysis with
                {
                    EvidenceSnapshots = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["diagnostics-kb"] = string.Create(
                            System.Globalization.CultureInfo.InvariantCulture,
                            $"schema{DiagnosticKnowledgeBase.SchemaVersion}:{knowledge.Entries.Count} entries")
                    }
                }
            };
        }

        var environment = new DiagnosticEnvironment(
            selectedSerial ?? session.GameSerial,
            session.AppVersion,
            session.PpuHashes,
            build is { IsExact: true, BuildNumber: { } number } ? number : null,
            hostPlatform,
            hardware?.OsArchitecture?.ToLowerInvariant(),
            GpuVendor(hardware),
            session.Renderer);

        cancellationToken.ThrowIfCancellationRequested();
        var report = DiagnosticEngine.Evaluate(session, environment, knowledge);
        cancellationToken.ThrowIfCancellationRequested();
        return new LogDiagnosticsResult(
            report with { Notes = [.. report.Notes.Where(note => !notes.Contains(note)), .. notes] },
            read.Sessions,
            notes,
            knowledgeError);
    }

    // A single proven vendor across every adapter; otherwise unknown (RPCS3's
    // own adapter is not assumed).
    private static string? GpuVendor(HwInfo? hardware)
    {
        var vendors = hardware?.GpuAdapters.Select(adapter => adapter.Vendor).Distinct().ToArray();
        return vendors is { Length: 1 } && vendors[0] is HwVendor.Amd or HwVendor.Nvidia or HwVendor.Intel
            ? vendors[0] switch { HwVendor.Amd => "AMD", HwVendor.Nvidia => "NVIDIA", _ => "Intel" }
            : null;
    }

    private static bool Same(string? left, string? right) =>
        left is not null && right is not null && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
