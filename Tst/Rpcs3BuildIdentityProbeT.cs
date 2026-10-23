using System;
using System.IO;
using System.Linq;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;

// Real regression coverage for Core\Svc\Scan\Rpcs3BuildIdentityProbe.cs and
// Rpcs3BuildIdentityParser.cs. This feature previously had an implementation
// with zero test coverage and four declared Rpcs3BuildIdentityState values
// (Unavailable, Partial, Exact, Conflict) of which only Unavailable and
// Exact were ever actually produced. Partial/Conflict are now real,
// evidence-driven outcomes (see Rpcs3BuildIdentityProbe.Probe) and are
// covered here alongside the previously-existing Unavailable/Exact paths.
internal static class Rpcs3BuildIdentityProbeT
{
    private const string ExactBanner =
        "RPCS3 v0.0.35-17000-abcdef1234567890abcdef1234567890abcdef12 HEAD | master";

    private const string NewerBanner =
        "RPCS3 v0.0.36-17500-1234567890abcdef1234567890abcdef12345678 HEAD | master";

    public static void Run(string tempRoot)
    {
        var root = Path.Combine(tempRoot, "Rpcs3BuildIdentity");
        Directory.CreateDirectory(root);

        Run_Unavailable_NoLogFiles(root);
        Run_Unavailable_UnsupportedBanner(root);
        Run_Exact_SingleLog(root);
        Run_Exact_Architecture_IsNotHardcoded(root);
        Run_Partial_UnparseableSecondaryLog(root);
        Run_Conflict_DisagreeingLogs(root);
        Run_Exact_PortableStyleSingleLogPath(root);
        Run_UiTip_CoversEveryStateWithoutThrowing();
    }

    // Regression for the UI consumer wiring: Rpcs3BuildIdentity was produced
    // and stored in AnalyzeResult (see AnalysisUseCase / Program.SmokeAnalysis)
    // but had zero UI consumer. UiText.Rpcs3BuildTip is the status-bar
    // formatter that closes that gap; it must handle every declared state
    // (including "not scanned yet") without throwing and must surface the
    // actual evidence rather than a generic placeholder.
    private static void Run_UiTip_CoversEveryStateWithoutThrowing()
    {
        var notScanned = UiText.Rpcs3BuildTip(null);
        Assert(!string.IsNullOrWhiteSpace(notScanned),
            "Expected a not-scanned RPCS3 build tip before analysis has run.");

        var unavailable = UiText.Rpcs3BuildTip(
            Rpcs3BuildIdentity.Unavailable("RPCS3.log was not found."));
        Assert(unavailable.Contains("RPCS3.log was not found.", StringComparison.Ordinal),
            "Expected the Unavailable tip to surface the actual diagnostic, not a generic message.");

        var exact = UiText.Rpcs3BuildTip(new Rpcs3BuildIdentity(
            VersionText: "0.0.35",
            BuildNumber: 17000,
            Commit: "abcdef1234567890abcdef1234567890abcdef12",
            Stage: "HEAD",
            Branch: "master",
            Architecture: "x64",
            State: Rpcs3BuildIdentityState.Exact,
            Evidence: []));
        Assert(exact.Contains("0.0.35", StringComparison.Ordinal) &&
               exact.Contains("17000", StringComparison.Ordinal) &&
               exact.Contains("abcdef1234567890abcdef1234567890abcdef12", StringComparison.Ordinal),
            "Expected the Exact tip to surface version, build number, and commit evidence.");

        var partial = UiText.Rpcs3BuildTip(new Rpcs3BuildIdentity(
            VersionText: "0.0.35",
            BuildNumber: 17000,
            Commit: "abcdef1234567890abcdef1234567890abcdef12",
            Stage: "HEAD",
            Branch: "master",
            Architecture: "x64",
            State: Rpcs3BuildIdentityState.Partial,
            Evidence: [],
            Diagnostic: "1 additional RPCS3 log file(s) were found without a supported build banner."));
        Assert(partial.Contains("0.0.35", StringComparison.Ordinal) &&
               partial.Contains("without a supported build banner", StringComparison.Ordinal),
            "Expected the Partial tip to surface both the best-known identity and the partial-evidence diagnostic.");

        var conflict = UiText.Rpcs3BuildTip(new Rpcs3BuildIdentity(
            VersionText: "0.0.36",
            BuildNumber: 17500,
            Commit: "1234567890abcdef1234567890abcdef12345678",
            Stage: "HEAD",
            Branch: "master",
            Architecture: "x64",
            State: Rpcs3BuildIdentityState.Conflict,
            Evidence: [],
            Diagnostic: "2 RPCS3 log files reported different build identities."));
        Assert(conflict.Contains("2 RPCS3 log files reported different build identities.", StringComparison.Ordinal),
            "Expected the Conflict tip to surface the disagreement diagnostic rather than a single misleading identity.");

        Assert(new[] { notScanned, unavailable, exact, partial, conflict }.Distinct().Count() == 5,
            "Expected every RPCS3 build identity state to produce a distinct UI tip.");
    }

    private static Rpcs3InstallDescriptor MakeInstall(string root)
    {
        var configPath = Path.Combine(root, "config");
        var customPath = Path.Combine(configPath, "custom_configs");
        var patchPath = Path.Combine(root, "patches");
        Directory.CreateDirectory(customPath);
        Directory.CreateDirectory(patchPath);

        return new Rpcs3InstallDescriptor(
            root,
            Path.Combine(root, "rpcs3.exe"),
            configPath,
            customPath,
            patchPath,
            Path.Combine(configPath, "patch_config.yml"));
    }

    private static void Run_Unavailable_NoLogFiles(string tempRoot)
    {
        var caseRoot = Path.Combine(tempRoot, "Unavailable-NoLogFiles");
        Directory.CreateDirectory(caseRoot);
        var install = MakeInstall(caseRoot);

        var identity = new Rpcs3BuildIdentityProbe().Probe(install);

        Assert(identity.State == Rpcs3BuildIdentityState.Unavailable &&
               identity.VersionText is null &&
               identity.Evidence.Count == 0,
            "Expected a missing RPCS3.log (portable and root layouts both absent) to resolve to Unavailable with no evidence.");
    }

    private static void Run_Unavailable_UnsupportedBanner(string tempRoot)
    {
        var caseRoot = Path.Combine(tempRoot, "Unavailable-BadBanner");
        var logDir = Path.Combine(caseRoot, "log");
        Directory.CreateDirectory(logDir);
        File.WriteAllText(
            Path.Combine(logDir, "RPCS3.log"),
            "RPCS3 starting up..." + Environment.NewLine + "Not a recognized build banner." + Environment.NewLine);
        var install = MakeInstall(caseRoot);

        var identity = new Rpcs3BuildIdentityProbe().Probe(install);

        Assert(identity.State == Rpcs3BuildIdentityState.Unavailable &&
               !string.IsNullOrWhiteSpace(identity.Diagnostic),
            "Expected an RPCS3.log without a supported build banner to resolve to Unavailable with a diagnostic, not throw or fabricate identity.");
    }

    private static void Run_Exact_SingleLog(string tempRoot)
    {
        var caseRoot = Path.Combine(tempRoot, "Exact-SingleLog");
        var logDir = Path.Combine(caseRoot, "log");
        Directory.CreateDirectory(logDir);
        File.WriteAllText(
            Path.Combine(logDir, "RPCS3.log"),
            ExactBanner + Environment.NewLine + "Architecture: x64" + Environment.NewLine);
        var install = MakeInstall(caseRoot);

        var identity = new Rpcs3BuildIdentityProbe().Probe(install);

        Assert(identity.State == Rpcs3BuildIdentityState.Exact &&
               identity.IsExact &&
               identity.VersionText == "0.0.35" &&
               identity.BuildNumber == 17000 &&
               identity.Commit == "abcdef1234567890abcdef1234567890abcdef12" &&
               identity.Architecture == "x64" &&
               identity.Evidence.Count == 1,
            "Expected a single well-formed RPCS3.log to resolve to Exact with the parsed banner fields preserved.");
    }

    private static void Run_Exact_Architecture_IsNotHardcoded(string tempRoot)
    {
        var caseRoot = Path.Combine(tempRoot, "Exact-NonX64Architecture");
        var logDir = Path.Combine(caseRoot, "log");
        Directory.CreateDirectory(logDir);
        File.WriteAllText(
            Path.Combine(logDir, "RPCS3.log"),
            ExactBanner + Environment.NewLine + "Architecture: arm64" + Environment.NewLine);
        var install = MakeInstall(caseRoot);

        var identity = new Rpcs3BuildIdentityProbe().Probe(install);

        Assert(identity.State == Rpcs3BuildIdentityState.Exact &&
               identity.Architecture == "arm64",
            "Expected the architecture probe to read whatever RPCS3.log reports rather than assuming x64.");
    }

    private static void Run_Partial_UnparseableSecondaryLog(string tempRoot)
    {
        var caseRoot = Path.Combine(tempRoot, "Partial-UnparseableSecondary");
        var logDir = Path.Combine(caseRoot, "log");
        Directory.CreateDirectory(logDir);
        File.WriteAllText(
            Path.Combine(logDir, "RPCS3.log"),
            ExactBanner + Environment.NewLine + "Architecture: x64" + Environment.NewLine);
        // A stray root-level RPCS3.log with no recognizable banner — present,
        // but it cannot corroborate or contradict the active log\RPCS3.log.
        File.WriteAllText(
            Path.Combine(caseRoot, "RPCS3.log"),
            "(truncated)" + Environment.NewLine);
        var install = MakeInstall(caseRoot);

        var identity = new Rpcs3BuildIdentityProbe().Probe(install);

        Assert(identity.State == Rpcs3BuildIdentityState.Partial &&
               identity.VersionText == "0.0.35" &&
               identity.Evidence.Count == 1 &&
               !string.IsNullOrWhiteSpace(identity.Diagnostic),
            "Expected one readable build banner alongside one unreadable/unsupported log file to resolve to Partial, not silently be treated as Exact.");
    }

    private static void Run_Conflict_DisagreeingLogs(string tempRoot)
    {
        var caseRoot = Path.Combine(tempRoot, "Conflict-DisagreeingLogs");
        var logDir = Path.Combine(caseRoot, "log");
        Directory.CreateDirectory(logDir);
        File.WriteAllText(
            Path.Combine(logDir, "RPCS3.log"),
            NewerBanner + Environment.NewLine + "Architecture: x64" + Environment.NewLine);
        // An older root-level RPCS3.log left over from a previous build,
        // reporting a genuinely different version/build/commit.
        File.WriteAllText(
            Path.Combine(caseRoot, "RPCS3.log"),
            ExactBanner + Environment.NewLine + "Architecture: x64" + Environment.NewLine);
        File.SetLastWriteTimeUtc(
            Path.Combine(caseRoot, "RPCS3.log"),
            DateTime.UtcNow.AddDays(-3));
        var install = MakeInstall(caseRoot);

        var identity = new Rpcs3BuildIdentityProbe().Probe(install);

        Assert(identity.State == Rpcs3BuildIdentityState.Conflict &&
               identity.Evidence.Count == 2 &&
               identity.Evidence.Select(item => item.BuildNumber).Distinct().Count() == 2 &&
               !string.IsNullOrWhiteSpace(identity.Diagnostic),
            "Expected two RPCS3.log files that disagree on version/build/commit to resolve to Conflict with both pieces of evidence retained, not silently pick the newest one.");
    }

    private static void Run_Exact_PortableStyleSingleLogPath(string tempRoot)
    {
        // A portable-style layout where only the root-level RPCS3.log exists
        // (no log\ subdirectory) must still resolve to Exact from a single
        // candidate path, exercising the non-default branch of
        // Rpcs3LogLocator.PlainLogPaths without hardcoding a layout.
        var caseRoot = Path.Combine(tempRoot, "Exact-PortableSingleLog");
        Directory.CreateDirectory(caseRoot);
        File.WriteAllText(
            Path.Combine(caseRoot, "RPCS3.log"),
            ExactBanner + Environment.NewLine + "Architecture: x64" + Environment.NewLine);
        var install = MakeInstall(caseRoot);

        var identity = new Rpcs3BuildIdentityProbe().Probe(install);

        Assert(identity.State == Rpcs3BuildIdentityState.Exact &&
               identity.Evidence.Count == 1 &&
               identity.Evidence[0].SourcePath.EndsWith("RPCS3.log", StringComparison.OrdinalIgnoreCase),
            "Expected a portable-style install with only a root-level RPCS3.log to resolve to Exact from that single candidate.");
    }
}
