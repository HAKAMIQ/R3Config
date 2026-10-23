using System.IO;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Application;

public sealed record WikiEvidenceLoadResult(
    WikiEvidenceCatalog Catalog,
    string? Error);

// A rejected evidence catalog loads as empty (never partially) and keeps the
// deterministic rejection reason so it can be shown to the developer.
public sealed record DiagnosticEvidenceLoadResult(
    DiagnosticEvidenceCatalog Catalog,
    string? Error);

// The generated knowledge file is optional data: a rejected file loads as empty with its reason.
public sealed record KnowledgeLoadResult(
    Rpcs3KnowledgeCatalog Catalog,
    string? Error);

// Read-only consumer path for Wiki notes/online evidence. It never writes a
// file and never touches RPCS3; a missing sidecar is the normal "no evidence
// available" state, not an error.
public sealed class WikiEvidenceUseCase(
    string hostPlatform = "Windows",
    string fileName = WikiEvidenceCatalog.FileName,
    string buildCatalogFileName = Rpcs3BuildCatalog.FileName,
    string diagnosticEvidenceFileName = DiagnosticEvidenceCatalog.FileName,
    string knowledgeFileName = Rpcs3KnowledgeCatalog.FileName)
{
    public const string InvalidMessage = "Wiki notes data is invalid";

    public WikiEvidenceLoadResult Load(string baseDirectory)
    {
        var path = Path.Combine(
            baseDirectory,
            "Data",
            fileName);

        if (!File.Exists(path))
        {
            return new WikiEvidenceLoadResult(WikiEvidenceCatalog.Empty, null);
        }

        try
        {
            return new WikiEvidenceLoadResult(
                WikiEvidenceCatalog.Load(path),
                null);
        }
        catch
        {
            return new WikiEvidenceLoadResult(
                WikiEvidenceCatalog.Empty,
                InvalidMessage);
        }
    }

    // The build catalog is optional data: without it no exact download is
    // offered, and nothing else changes.
    public Rpcs3BuildCatalog LoadBuildCatalog(string baseDirectory)
    {
        var path = Path.Combine(baseDirectory, "Data", buildCatalogFileName);

        try
        {
            return File.Exists(path)
                ? Rpcs3BuildCatalog.Load(path)
                : Rpcs3BuildCatalog.Empty;
        }
        catch
        {
            return Rpcs3BuildCatalog.Empty;
        }
    }

    // Compact RPCN diagnostic evidence is optional data too; a missing or
    // invalid file means no source-separated RPCN evidence, nothing else.
    public DiagnosticEvidenceLoadResult LoadDiagnosticEvidence(string baseDirectory)
    {
        var path = Path.Combine(baseDirectory, "Data", diagnosticEvidenceFileName);

        try
        {
            return new DiagnosticEvidenceLoadResult(
                File.Exists(path)
                    ? DiagnosticEvidenceCatalog.Load(path)
                    : DiagnosticEvidenceCatalog.Empty,
                null);
        }
        catch (DiagnosticEvidenceRejectedException ex)
        {
            return new DiagnosticEvidenceLoadResult(DiagnosticEvidenceCatalog.Empty, ex.Message);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return new DiagnosticEvidenceLoadResult(
                DiagnosticEvidenceCatalog.Empty,
                $"Diagnostic evidence could not be read: {ex.Message}");
        }
    }

    public KnowledgeLoadResult LoadKnowledge(string baseDirectory)
    {
        var path = Path.Combine(baseDirectory, "Data", knowledgeFileName);

        try
        {
            return new KnowledgeLoadResult(
                File.Exists(path)
                    ? Rpcs3KnowledgeCatalog.Load(path)
                    : Rpcs3KnowledgeCatalog.Empty,
                null);
        }
        catch (Rpcs3KnowledgeRejectedException ex)
        {
            return new KnowledgeLoadResult(Rpcs3KnowledgeCatalog.Empty, ex.Message);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new KnowledgeLoadResult(
                Rpcs3KnowledgeCatalog.Empty,
                $"Knowledge could not be read: {ex.Message}");
        }
    }

    // The installed build exactly as the Runtime Context identified it; a
    // partial or conflicting identity is not shown as "current".
    private static string? CurrentBuildText(Rpcs3BuildIdentity? build) =>
        build is { IsExact: true, BuildNumber: { } number }
            ? $"{build.VersionText}-{number}"
            : null;

    public GameSettingsModel Project(
        CfgPlan? plan,
        Rpcs3GameDescriptor game,
        WikiRec? recommendation,
        WikiEvidenceCatalog evidence,
        Rpcs3BuildIdentity? build,
        HwInfo? hardware = null,
        Rpcs3BuildCatalog? builds = null,
        Rpcs3KnowledgeCatalog? knowledge = null)
    {
        var context = new GameSettingsContext(
            game.Serial,
            game.Version,
            hostPlatform,
            Rpcs3RuntimeBuild.From(build),
            hardware?.GpuName,
            hardware?.CpuName,
            CurrentBuildText(build),
            hardware);

        var model = GameSettingsProjector.Project(
            plan,
            evidence.Find(game.Serial, recommendation?.WikiName),
            context,
            builds);

        return model with
        {
            Knowledge = knowledge?.ForGame(game.Serial, recommendation) ?? GameKnowledge.Empty
        };
    }
}
