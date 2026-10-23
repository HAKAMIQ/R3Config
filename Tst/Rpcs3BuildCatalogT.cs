using System;
using System.IO;
using System.Linq;
using R3Config.Application;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;
using R3Config.Vm;

// Regression for the generic tested-RPCS3-build resolver and its wiring into
// the compact RPCN block. Every fixture is synthetic: the resolver must work
// for any game and any "X.Y.Z-BUILD" text, so nothing here (or in production)
// depends on one title, serial, game version or build number.
internal static class Rpcs3BuildCatalogT
{
    private const string Repo = "https://github.com/RPCS3/rpcs3-binaries-win/releases/download/";

    private static string Url(string commit, string file) => $"{Repo}build-{commit}/{file}";

    private static readonly string Url16004 =
        Url("0123456789abcdef0123456789abcdef01234567", "rpcs3-v0.0.30-16004-01234567_win64_msvc.7z");

    private static readonly string Url19000 =
        Url("89abcdef0123456789abcdef0123456789abcdef", "rpcs3-v0.0.41-19000-89abcdef_win64_msvc.7z");

    public static void Run()
    {
        AssertParsing();
        AssertResolution();
        AssertFailClosed();
        AssertSourceClasses();
        AssertProjectionForNonMinecraftGame();
        AssertShippedCatalog();
        AssertLoader();

        Console.WriteLine("RPCS3 tested-build catalog (generic parse, exact resolve, fail-closed, platform guard, source classes, non-Minecraft projection, shipped data) PASS.");
    }

    private static Rpcs3BuildCatalog Catalog(params Rpcs3BuildEntry[] entries) => new(entries);

    private static Rpcs3BuildEntry Windows(string version, int build, string url) =>
        new(version, build, "Windows", url);

    private static void AssertParsing()
    {
        Assert(Rpcs3BuildCatalog.TryParse("0.0.30-16004", out var version, out var build) &&
               version == "0.0.30" && build == 16004,
            "0.0.30-16004 must parse to version 0.0.30 and build 16004.");
        Assert(Rpcs3BuildCatalog.TryParse(" v0.0.41-19000 ", out version, out build) &&
               version == "0.0.41" && build == 19000,
            "Any X.Y.Z-BUILD text (with an optional leading v and padding) must parse generically.");
        Assert(Rpcs3BuildCatalog.TryParse("1.2.3-7", out version, out build) &&
               version == "1.2.3" && build == 7,
            "The parser must not assume a 0.0.x version or a 5-digit build.");

        foreach (var bad in new[] { null, "", "0.0.30", "0.0.30-", "0.0-16004", "abc-16004", "0.0.30-16004-x", "0.0.30-99999999999" })
        {
            Assert(!Rpcs3BuildCatalog.TryParse(bad, out _, out _),
                $"'{bad}' is not an X.Y.Z-BUILD text and must not parse.");
        }
    }

    private static void AssertResolution()
    {
        var catalog = Catalog(
            Windows("0.0.30", 16004, Url16004),
            Windows("0.0.41", 19000, Url19000));

        Assert(catalog.ResolveDownloadUrl("0.0.30-16004", "Windows") == Url16004,
            "An exact Version+Build row must resolve its exact download URL.");
        Assert(catalog.ResolveDownloadUrl("0.0.41-19000", "windows") == Url19000,
            "A second arbitrary version/build must resolve too (platform match is case-insensitive).");

        var identical = Catalog(
            Windows("0.0.30", 16004, Url16004),
            Windows("0.0.30", 16004, Url16004));
        Assert(identical.ResolveDownloadUrl("0.0.30-16004", "Windows") == Url16004,
            "A repeated identical row is not a conflict.");
    }

    private static void AssertFailClosed()
    {
        var catalog = Catalog(Windows("0.0.30", 16004, Url16004));

        Assert(catalog.ResolveDownloadUrl("0.0.30-16005", "Windows") is null &&
               catalog.ResolveDownloadUrl("0.0.31-16004", "Windows") is null,
            "A missing build (or the right build under another version) must resolve nothing.");
        Assert(catalog.ResolveDownloadUrl("0.0.30", "Windows") is null &&
               catalog.ResolveDownloadUrl(null, "Windows") is null,
            "Text that is not X.Y.Z-BUILD must resolve nothing.");

        var conflicting = Catalog(
            Windows("0.0.30", 16004, Url16004),
            Windows("0.0.30", 16004, Url19000));
        Assert(conflicting.ResolveDownloadUrl("0.0.30-16004", "Windows") is null,
            "Conflicting rows for one build must resolve nothing.");

        foreach (var bad in new[]
                 {
                     "not a url",
                     "https:///rpcs3-v0.0.30-16004.7z",
                     "http://github.com/RPCS3/rpcs3-binaries-win/releases/download/build-1/a.7z",
                     "https://evil.example/RPCS3/rpcs3-binaries-win/releases/download/build-1/a.7z",
                     "https://github.com.evil.example/RPCS3/rpcs3-binaries-win/releases/download/build-1/a.7z",
                     "https://user@github.com/RPCS3/rpcs3-binaries-win/releases/download/build-1/a.7z",
                     "https://github.com/Other/rpcs3-binaries-win/releases/download/build-1/a.7z",
                     "https://github.com/RPCS3/rpcs3-binaries-linux/releases/download/build-1/a.AppImage",
                     "https://github.com/RPCS3/rpcs3-binaries-win/releases/latest",
                     "https://rpcs3.net/download",
                     "https://archive.org/download/rpcs3-build-archive-windows-x64-v0.0.5/rpcs3-v0.0.5-6461-d78b0467_win64.7z",
                     "javascript:alert(1)",
                     "file:///C:/rpcs3.7z",
                     "ftp://github.com/RPCS3/rpcs3-binaries-win/releases/download/build-1/a.7z"
                 })
        {
            Assert(Catalog(Windows("0.0.30", 16004, bad)).ResolveDownloadUrl("0.0.30-16004", "Windows") is null,
                $"'{bad}' is not an official binaries URL and must resolve nothing.");
        }

        foreach (var platform in new[] { "macOS", "Linux", "", null })
        {
            Assert(catalog.ResolveDownloadUrl("0.0.30-16004", platform) is null,
                $"A Windows archive must not be offered on '{platform}'.");
        }

        var macRow = Catalog(new Rpcs3BuildEntry("0.0.30", 16004, "macOS", Url16004));
        Assert(macRow.ResolveDownloadUrl("0.0.30-16004", "Windows") is null,
            "A non-Windows row must not be used for a Windows host.");
        Assert(Rpcs3BuildCatalog.Empty.ResolveDownloadUrl("0.0.30-16004", "Windows") is null,
            "An empty catalog resolves nothing.");
    }

    // GitHub is the only downloadable source. Historical Internet Archive rows
    // are evidence: they never produce a download, never beat a GitHub row, and
    // only verified ones are even recognized.
    private static void AssertSourceClasses()
    {
        const string archiveUrl =
            "https://archive.org/download/rpcs3-build-archive-windows-x64-v0.0.5/rpcs3-v0.0.5-6461-d78b0467_win64.7z";

        Rpcs3BuildEntry Archive(string version, int build, string verification) =>
            new(version, build, "Windows", archiveUrl,
                SourceClass: Rpcs3BuildCatalog.ArchivePointerClass,
                MatchMethod: "EXACT_PR_COMMIT",
                Verification: verification);

        var verified = Catalog(Archive("0.0.5", 6461, Rpcs3BuildCatalog.ArchiveMetadataVerified));
        var resolution = verified.Resolve("0.0.5-6461", "Windows");
        Assert(resolution.State == Rpcs3BuildResolutionState.HistoricalArchiveEvidenceOnly &&
               resolution.DownloadUrl is null &&
               verified.ResolveDownloadUrl("0.0.5-6461", "Windows") is null,
            "A verified Archive row is recognized as historical evidence only and never yields a download URL.");
        Assert(Catalog(Archive("0.0.5", 6461, Rpcs3BuildCatalog.ArchiveIdentityVerified))
                   .Resolve("0.0.5-6461", "Windows").State == Rpcs3BuildResolutionState.HistoricalArchiveEvidenceOnly,
            "An identity-verified Archive row is recognized as historical evidence only.");

        foreach (var level in new[] { "ARCHIVE_AMBIGUOUS", "ARCHIVE_NOT_FOUND", "ARCHIVE_CONFLICT", "HASH_VERIFIED", null })
        {
            Assert(Catalog(Archive("0.0.5", 6461, level!)).Resolve("0.0.5-6461", "Windows").State ==
                   Rpcs3BuildResolutionState.Unresolved,
                $"An Archive row verified as '{level}' must stay unresolved.");
        }

        Assert(Catalog(
                   Archive("0.0.5", 6461, Rpcs3BuildCatalog.ArchiveMetadataVerified),
                   Archive("0.0.5", 6461, Rpcs3BuildCatalog.ArchiveIdentityVerified))
               .Resolve("0.0.5-6461", "Windows").State == Rpcs3BuildResolutionState.Unresolved,
            "Two Archive rows for one build are ambiguous and stay unresolved.");

        var both = Catalog(
            Archive("0.0.5", 6461, Rpcs3BuildCatalog.ArchiveMetadataVerified),
            Windows("0.0.5", 6461, Url16004));
        var githubWins = both.Resolve("0.0.5-6461", "Windows");
        Assert(githubWins.State == Rpcs3BuildResolutionState.ResolvedOfficialGitHub &&
               githubWins.DownloadUrl == Url16004,
            "An exact official GitHub binary always wins over Archive evidence.");

        Assert(Catalog(Archive("0.0.5", 6461, Rpcs3BuildCatalog.ArchiveMetadataVerified))
                   .Resolve("0.0.5-6461", "macOS").State == Rpcs3BuildResolutionState.Unresolved,
            "Archive evidence is not resolved for a non-Windows host either.");

        var legacy = new Rpcs3BuildEntry("0.0.29", 15565, "Windows",
            Url("7e281a3354aaface59af02fe95836f63727e96dd", "rpcs3-v0.0.29-7e281a33_win64.7z"),
            SourceClass: Rpcs3BuildCatalog.GitHubDirectClass, MatchMethod: "OFFICIAL_URL_MATCH");
        var legacyResolution = Catalog(legacy).Resolve("0.0.29-15565", "Windows");
        Assert(legacyResolution.State == Rpcs3BuildResolutionState.ResolvedOfficialGitHub &&
               legacyResolution.Reason == "OFFICIAL_URL_MATCH" &&
               Catalog(legacy).Resolve("0.0.29-15566", "Windows").State == Rpcs3BuildResolutionState.Unresolved,
            "An explicit official-URL mapping resolves exactly its own build and nothing near it.");
    }

    // A game that is not Minecraft, with its own serial, game version, build.
    private static void AssertProjectionForNonMinecraftGame()
    {
        const string serial = "TEST00001";
        var page = new WikiPageEvidence(
            "Synthetic Game",
            [serial],
            [],
            [new WikiOnlineEvidence(
                "Synthetic Game",
                WikiOnlineState.Supported,
                "2.10",
                null,
                "0.0.41-19000",
                "Yes",
                "Yes",
                "Yes",
                "Yes",
                "Not required",
                null)]);
        var builds = Catalog(Windows("0.0.41", 19000, Url19000));

        GameEvidenceVm Load(GameSettingsContext context)
        {
            var model = GameSettingsProjector.Project(null, page, context, builds);
            var vm = new GameEvidenceVm();
            vm.Load(model);
            return vm;
        }

        UiText.SetLang(UiText.EnLang);
        var matched = Load(new GameSettingsContext(serial, "2.10"));
        var online = matched.Online.Single();
        Assert(online.ExactBuildUrl == Url19000 &&
               online.SummaryRows.Single(row => row.HasAction).ActionUrl == Url19000 &&
               online.SummaryRows.Single(row => row.HasAction).Id == "Rpcs3VersionTested" &&
               online.SummaryRows.Single(row => row.HasAction).Value == "0.0.41-19000" &&
               online.SummaryRows.Single(row => row.HasAction).ActionLabel == "Download build",
            "A non-Minecraft game must get the compact block with its exact-build download on the build row.");

        var mismatch = Load(new GameSettingsContext(serial, "2.11"));
        Assert(mismatch.HasOnlineMismatch && !mismatch.HasOnline && mismatch.Online.Count == 0,
            "A different installed version shows only the neutral mismatch line, with no build or download.");

        var unknown = Load(new GameSettingsContext(serial, null));
        Assert(unknown.HasOnlineUnknownVersion && !unknown.HasOnline && unknown.Online.Count == 0 &&
               !unknown.HasOnlineMismatch,
            "An unknown installed version shows only the cannot-confirm line, with no build or download.");

        var mac = Load(new GameSettingsContext(serial, "2.10", "macOS"));
        Assert(mac.Online.Single().ExactBuildUrl is null &&
               mac.Online.Single().SummaryRows.All(row => !row.HasAction),
            "A Windows archive must not be offered when the runtime context is not Windows.");

        var noCatalog = GameSettingsProjector.Project(null, page, new GameSettingsContext(serial, "2.10"));
        var noCatalogVm = new GameEvidenceVm();
        noCatalogVm.Load(noCatalog);
        Assert(noCatalogVm.Online.Single().ExactBuildUrl is null &&
               noCatalogVm.Online.Single().SummaryRows.Any(row => row.Id == "Rpcs3VersionTested"),
            "Without a catalog the build is still shown as plain text and no download is offered.");

        AssertCurrentBuild(page, builds);

        // Arabic: fixed labels are natural Arabic; the build number stays LTR data.
        UiText.SetLang(UiText.ArabLang);
        try
        {
            matched.SetLang();
            var arabic = matched.Online.Single();
            var buildRow = arabic.SummaryRows.Single(row => row.HasAction);
            Assert(buildRow.Label == "نسخة RPCS3 المختبرة" &&
                   buildRow.ActionLabel == "تنزيل النسخة" &&
                   arabic.SummaryRows[0].Label == "اللعب عبر RPCN" &&
                   buildRow.Value == "0.0.41-19000",
                "Arabic must show the natural labels with the build number untouched.");
        }
        finally
        {
            UiText.SetLang(UiText.EnLang);
        }
    }

    // The installed build comes only from the Runtime Context; the tested build
    // only from the applicable evidence. Different builds show both, distinctly
    // and informationally; identical builds show no duplicate and no download.
    private static void AssertCurrentBuild(WikiPageEvidence page, Rpcs3BuildCatalog builds)
    {
        GameOnlineVm Online(string? current, string? installedGame = "2.10")
        {
            var model = GameSettingsProjector.Project(
                null,
                page,
                new GameSettingsContext("TEST00001", installedGame, CurrentRpcs3Build: current),
                builds);
            var vm = new GameEvidenceVm();
            vm.Load(model);
            return vm.Online.Single();
        }

        var different = Online("0.0.42-19843");
        var ids = different.SummaryRows.Select(row => row.Id).ToArray();
        Assert(ids.SequenceEqual(["State", "TestedGameVersion", "CurrentRpcs3Build", "Rpcs3VersionTested", "RequiresCustomServers"]) &&
               different.SummaryRows.Single(row => row.Id == "CurrentRpcs3Build").Value == "0.0.42-19843" &&
               different.SummaryRows.Single(row => row.Id == "CurrentRpcs3Build").Label == "Current RPCS3 build" &&
               !different.SummaryRows.Single(row => row.Id == "CurrentRpcs3Build").HasAction &&
               different.SummaryRows.Single(row => row.Id == "Rpcs3VersionTested").Value == "0.0.41-19000" &&
               different.SummaryRows.Single(row => row.Id == "Rpcs3VersionTested").ActionUrl == Url19000 &&
               different.Rows.All(row => row.Id != "RequiredGameVersion"),
            "Different current and tested builds show both distinctly; only the tested build carries the download; nothing is called required.");

        var same = Online("0.0.41-19000");
        Assert(same.TestedIsCurrent &&
               same.SummaryRows.All(row => row.Id != "CurrentRpcs3Build" && !row.HasAction) &&
               same.SummaryRows.Any(row => row.Id == "Rpcs3VersionTested"),
            "When the installed build is the tested build there is no duplicate row and no download action.");

        var unknown = Online(null);
        Assert(unknown.SummaryRows.All(row => row.Id != "CurrentRpcs3Build") &&
               unknown.SummaryRows.Single(row => row.Id == "Rpcs3VersionTested").HasAction,
            "An unknown installed build hides the current row and keeps the tested build with its download.");

        var partial = new WikiEvidenceUseCase().Project(
            null,
            new Rpcs3GameDescriptor("TEST00001", "Synthetic Game", string.Empty, "2.10", null, null),
            null,
            WikiEvidenceCatalog.FromPages("t", "t", "t", [page]),
            new Rpcs3BuildIdentity("0.0.42", 19843, null, null, null, null, Rpcs3BuildIdentityState.Partial, []));
        Assert(partial.CurrentRpcs3Build is null,
            "A partial Runtime Context build identity must not be shown as the current build.");

        var exact = new WikiEvidenceUseCase().Project(
            null,
            new Rpcs3GameDescriptor("TEST00001", "Synthetic Game", string.Empty, "2.10", null, null),
            null,
            WikiEvidenceCatalog.FromPages("t", "t", "t", [page]),
            new Rpcs3BuildIdentity("0.0.42", 19843, "abcdef1", "master", "master", "x64", Rpcs3BuildIdentityState.Exact, []));
        Assert(exact.CurrentRpcs3Build == "0.0.42-19843",
            "An exact Runtime Context build identity is the current build (version-build).");
    }

    // The shipped artifact is what production resolves from: it must load from
    // the build output alone (no F: workspace, no network), keep GitHub and
    // Archive rows strictly separate, and resolve exactly.
    private static void AssertShippedCatalog()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", Rpcs3BuildCatalog.FileName);
        Assert(File.Exists(path), "The build output must carry Data/rpcs3-builds.json.");
        var text = File.ReadAllText(path);
        var catalog = Rpcs3BuildCatalog.Load(path);

        var github = catalog.Builds.Where(entry => entry.SourceClass == Rpcs3BuildCatalog.GitHubDirectClass).ToArray();
        var archive = catalog.Builds.Where(entry => entry.SourceClass == Rpcs3BuildCatalog.ArchivePointerClass).ToArray();
        Assert(github.Length > 1000 && github.Length + archive.Length == catalog.Count,
            "Every shipped row is either an official GitHub row or a historical Archive evidence row.");
        Assert(catalog.Builds.Select(entry => (entry.Version, entry.Build, entry.SourceClass)).Distinct().Count() == catalog.Count &&
               github.Select(entry => (entry.Version, entry.Build)).Distinct().Count() == github.Length,
            "The shipped catalog must hold one row per Version+Build and source class (no silent duplicates).");

        // Real production control: 0.0.30-16004 straight from the shipped data.
        var control = github.Single(entry => entry.Version == "0.0.30" && entry.Build == 16004);
        Assert(control.DownloadUrl ==
                   "https://github.com/RPCS3/rpcs3-binaries-win/releases/download/build-0d1fbfb755aa7856a8b94c74be2e116a0ca743cb/rpcs3-v0.0.30-16004-0d1fbfb7_win64.7z" &&
               control.CommitFull == "0d1fbfb755aa7856a8b94c74be2e116a0ca743cb" &&
               control.SizeBytes == 29047599 &&
               control.Sha256 == "C3A58C9EF803F51A88D25922EAC7F160B7FFE880EA3C947008C981C6ED44C421" &&
               control.MatchMethod == "EXACT_VERSION_BUILD" &&
               catalog.ResolveDownloadUrl("0.0.30-16004", "Windows") == control.DownloadUrl &&
               catalog.ResolveDownloadUrl("0.0.30-16003", "Windows") is null,
            "0.0.30-16004 resolves from the shipped data to its exact commit, size, SHA-256 and URL; an adjacent absent build resolves to nothing.");

        // Legacy official-URL mapping: Build 15565 is kept although the old
        // GitHub filename carries no build number.
        var legacy = catalog.Resolve("0.0.29-15565", "Windows");
        Assert(legacy.State == Rpcs3BuildResolutionState.ResolvedOfficialGitHub &&
               legacy.Entry!.Build == 15565 &&
               legacy.Entry.MatchMethod == "OFFICIAL_URL_MATCH" &&
               legacy.DownloadUrl!.EndsWith("/rpcs3-v0.0.29-7e281a33_win64.7z", StringComparison.Ordinal),
            "0.0.29-15565 resolves through the explicit OFFICIAL_URL_MATCH mapping.");

        foreach (var entry in github)
        {
            Assert(entry.Platform == Rpcs3BuildCatalog.WindowsPlatform &&
                   Rpcs3BuildCatalog.IsOfficialWindowsBinaryUrl(entry.DownloadUrl) &&
                   catalog.ResolveDownloadUrl($"{entry.Version}-{entry.Build}", "Windows") == entry.DownloadUrl &&
                   catalog.ResolveDownloadUrl($"{entry.Version}-{entry.Build}", "Linux") is null,
                $"Shipped GitHub row {entry.Version}-{entry.Build} must be an official GitHub Windows URL that resolves to itself.");
        }

        // Historical Archive rows are reference evidence: verified only, never
        // hash verified, never downloadable, never beating GitHub.
        foreach (var entry in archive)
        {
            var resolution = catalog.Resolve($"{entry.Version}-{entry.Build}", "Windows");
            Assert(entry.Verification is Rpcs3BuildCatalog.ArchiveMetadataVerified or Rpcs3BuildCatalog.ArchiveIdentityVerified &&
                   entry.DownloadUrl.StartsWith("https://archive.org/download/", StringComparison.Ordinal) &&
                   !Rpcs3BuildCatalog.IsOfficialWindowsBinaryUrl(entry.DownloadUrl) &&
                   resolution.State == Rpcs3BuildResolutionState.HistoricalArchiveEvidenceOnly &&
                   resolution.DownloadUrl is null,
                $"Shipped Archive row {entry.Version}-{entry.Build} must be verified reference evidence with no download.");
        }

        Assert(!text.Contains("HASH_VERIFIED", StringComparison.Ordinal) &&
               catalog.Builds.All(entry => entry.Verification is null or Rpcs3BuildCatalog.ArchiveMetadataVerified or Rpcs3BuildCatalog.ArchiveIdentityVerified),
            "Ambiguous, unresolved and conflicting Archive records are not eligible and must not ship as rows; nothing is hash verified.");

        // A build absent from the catalog stays unresolved: no nearest, latest
        // or fuzzy fallback exists in production.
        Assert(catalog.Resolve("0.0.42-99999", "Windows").State == Rpcs3BuildResolutionState.Unresolved &&
               catalog.Resolve("0.0.0-1", "Windows").State == Rpcs3BuildResolutionState.Unresolved,
            "A missing build is Unresolved.");

        // The application must run with no development workspace.
        Assert(!text.Contains("Downloaded-Websites", StringComparison.OrdinalIgnoreCase) &&
               !System.Text.RegularExpressions.Regex.IsMatch(text, @"(?<![A-Za-z0-9])[A-Za-z]:[\/]"),
            "The shipped catalog must not carry a development drive path.");
        Assert(!text.Contains("SHA256URL", StringComparison.OrdinalIgnoreCase) &&
               !text.Contains("System.Object[]", StringComparison.Ordinal),
            "The shipped catalog must carry only what R3Config needs, without catalog noise.");
    }

    private static void AssertLoader()
    {
        var root = Path.Combine(Path.GetTempPath(), "R3BuildCatalogT-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "Data"));
            Assert(new WikiEvidenceUseCase().LoadBuildCatalog(root).Count == 0,
                "A missing build catalog is the normal no-download state.");

            File.WriteAllText(Path.Combine(root, "Data", Rpcs3BuildCatalog.FileName), "{ not json");
            Assert(new WikiEvidenceUseCase().LoadBuildCatalog(root).Count == 0,
                "A corrupt build catalog must fall back to no download, not fail.");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
