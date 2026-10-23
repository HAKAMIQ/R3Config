using System;
using System.Collections.Generic;
using R3Config.Application;
using R3Config.Mdl;
using R3Config.Svc;

// Typed RPCS3 runtime identity and build-scoped Wiki notes. A build scope such as
// "0.0.13-11475" is compared as version and build numbers against the runtime
// identity; it is never a substring test on a version string, and only an Exact
// runtime identity may decide it. Every fixture is synthetic.
internal static class Rpcs3BuildScopeT
{
    private const string Serial = "BLUS99002";

    private static Rpcs3RuntimeBuild Exact(string version, int build) =>
        new(version, build, "abcdef1234567890abcdef1234567890abcdef12", "master", "Alpha", "x64", Rpcs3BuildIdentityState.Exact);

    public static void Run()
    {
        AssertParserFacts();
        AssertRuntimeBuildFromIdentity();
        AssertBuildComparisonIsTypedAndDirectional();
        AssertOnlyExactIdentityDecidesScope();
        AssertProjectionAndUseCaseCarryTypedFacts();

        Console.WriteLine("RPCS3 build identity scope (typed version/build/commit, numeric directional comparison, Exact-only decisions, use-case wiring) PASS.");
    }

    private static void AssertParserFacts()
    {
        Assert(Rpcs3BuildIdentityParser.TryParse(["RPCS3 v0.0.35-17000-abcdef12 Alpha | master", "Architecture: x64"], out var shortCommit) &&
               shortCommit.VersionText == "0.0.35" && shortCommit.BuildNumber == 17000 && shortCommit.Commit == "abcdef12" &&
               shortCommit.Stage == "Alpha" && shortCommit.Branch == "master" && shortCommit.Architecture == "x64",
            "A banner with a short commit exposes version, build, commit, stage, branch and architecture as separate facts.");
        Assert(!Rpcs3BuildIdentityParser.TryParse(["RPCS3 v0.0.35 Alpha | master"], out _),
            "A version-only banner is not a build identity.");
        Assert(!Rpcs3BuildIdentityParser.TryParse(["RPCS3 v0.0.35-17000 Alpha | master"], out _),
            "A version+build banner without a commit is not an Exact-capable identity.");
        Assert(!Rpcs3BuildIdentityParser.TryParse(["not an rpcs3 banner"], out _) &&
               !Rpcs3BuildIdentityParser.TryParse([], out _) &&
               !Rpcs3BuildIdentityParser.TryParse(["RPCS3 v0.0.35-0-abcdef12 Alpha | master"], out _),
            "A malformed first line, an empty log and build 0 fail closed.");
        Assert(Rpcs3BuildIdentityParser.TryParse(["RPCS3 v0.0.35-17000-abcdef12 Alpha | master"], out var noArch) && noArch.Architecture is null,
            "A missing architecture line stays null instead of a default.");
    }

    private static void AssertRuntimeBuildFromIdentity()
    {
        Rpcs3BuildIdentity Identity(Rpcs3BuildIdentityState state) =>
            new("0.0.35", 17000, "abcdef12", "Alpha", "master", "x64", state, []);

        Assert(Rpcs3RuntimeBuild.From(null) is null && Rpcs3RuntimeBuild.From(Rpcs3BuildIdentity.Unavailable("none")) is null,
            "No identity and Unavailable produce no runtime build facts.");
        var exact = Rpcs3RuntimeBuild.From(Identity(Rpcs3BuildIdentityState.Exact))!;
        Assert(exact.IsExact && exact.Version == "0.0.35" && exact.BuildNumber == 17000 && exact.Commit == "abcdef12" &&
               exact.Branch == "master" && exact.Stage == "Alpha" && exact.Architecture == "x64",
            "An Exact identity keeps every fact typed and separate.");
        Assert(!Rpcs3RuntimeBuild.From(Identity(Rpcs3BuildIdentityState.Partial))!.IsExact &&
               !Rpcs3RuntimeBuild.From(Identity(Rpcs3BuildIdentityState.Conflict))!.IsExact,
            "Partial and Conflict identities are never Exact.");
        Assert(!new Rpcs3RuntimeBuild("0.0.35", null, "abcdef12", null, null, null, Rpcs3BuildIdentityState.Exact).IsExact,
            "An identity claiming Exact without a build number is still not Exact.");
    }

    private static void AssertBuildComparisonIsTypedAndDirectional()
    {
        // Real note wording from the Wiki corpus.
        const string Since = "RPCS3 issue #13253: There are graphical glitches since RPCS v0.0.26-14569 when both WCB and CPU blit are enabled.";
        const string After = "Black lines appear across the title screen in RPCS3 versions after v0.0.21-13399.";
        const string Before = "Fixed in builds before 0.0.30-16000.";
        const string Tested = "Tested on 0.0.13-11475 only.";

        bool? Since_(Rpcs3RuntimeBuild build) => NoteScope.Rpcs3BuildApplies(Since, "0.0.26-14569", build);
        Assert(Since_(Exact("0.0.35", 17000)) == true, "Newer version than 'since' applies.");
        Assert(Since_(Exact("0.0.26", 14569)) == true, "'since' includes the same build.");
        Assert(Since_(Exact("0.0.26", 14568)) == false, "One build before 'since' does not apply.");
        Assert(Since_(Exact("0.0.24", 99999)) == false, "An older version is older regardless of its build number.");

        Assert(NoteScope.Rpcs3BuildApplies(After, "0.0.21-13399", Exact("0.0.21", 13399)) == false &&
               NoteScope.Rpcs3BuildApplies(After, "0.0.21-13399", Exact("0.0.21", 13400)) == true,
            "'after' is strictly greater.");
        Assert(NoteScope.Rpcs3BuildApplies(Before, "0.0.30-16000", Exact("0.0.30", 15999)) == true &&
               NoteScope.Rpcs3BuildApplies(Before, "0.0.30-16000", Exact("0.0.30", 16000)) == false,
            "'before' is strictly smaller.");
        Assert(NoteScope.Rpcs3BuildApplies("Issue since 0.0.13-9999.", "0.0.13-9999", Exact("0.0.13", 10000)) == true,
            "Build numbers compare numerically, not as text (10000 > 9999).");

        Assert(NoteScope.Rpcs3BuildApplies(Tested, "0.0.13-11475", Exact("0.0.13", 11475)) == true,
            "A mention without direction applies only to that exact build.");
        Assert(NoteScope.Rpcs3BuildApplies(Tested, "0.0.13-11475", Exact("0.0.13", 11476)) is null &&
               NoteScope.Rpcs3BuildApplies(Tested, "0.0.13-11475", Exact("0.0.35", 17000)) is null,
            "A mention without direction never decides for another build: it stays unconfirmed.");
        Assert(NoteScope.Rpcs3BuildApplies("Seen on 0.0.1 builds.", "0.0.1", Exact("0.0.35", 17000)) is null &&
               NoteScope.Rpcs3BuildApplies("Seen on 0.0.13.", "0.0.13", Exact("0.0.13", 11475)) is null,
            "A version-only mention without direction is unconfirmed, even for the same version.");
        Assert(NoteScope.Rpcs3BuildApplies("since v0.0.26 only", "0.0.26", Exact("0.0.35", 17000)) == true,
            "A version-only 'since' compares version numbers.");
        Assert(NoteScope.Rpcs3BuildApplies("since not-a-version", "not-a-version", Exact("0.0.35", 17000)) is null,
            "An unparseable condition is unconfirmed.");
    }

    private static void AssertOnlyExactIdentityDecidesScope()
    {
        const string Since = "Glitches since v0.0.26-14569.";
        var partial = Exact("0.0.35", 17000) with { Confidence = Rpcs3BuildIdentityState.Partial };
        var conflict = Exact("0.0.35", 17000) with { Confidence = Rpcs3BuildIdentityState.Conflict };
        Assert(NoteScope.Rpcs3BuildApplies(Since, "0.0.26-14569", null) is null &&
               NoteScope.Rpcs3BuildApplies(Since, "0.0.26-14569", partial) is null &&
               NoteScope.Rpcs3BuildApplies(Since, "0.0.26-14569", conflict) is null,
            "Missing, Partial and Conflict runtime identities never decide a build scope (the log may be stale or disagree).");
    }

    private static WikiFreeTextNote Note(string text, string? buildCondition, string? versionCondition = null) =>
        new("Synthetic", "Known Issues", WikiNoteClass.InformationalNote, text, versionCondition, null, Rpcs3BuildCondition: buildCondition);

    private static void AssertProjectionAndUseCaseCarryTypedFacts()
    {
        var note = Note("Glitches since RPCS v0.0.26-14569 with CPU blit.", "0.0.26-14569");
        Assert(GameSettingsProjector.Applicability(note, new GameSettingsContext(Serial, RuntimeBuild: Exact("0.0.35", 17000))) == WikiNoteApplicability.General &&
               GameSettingsProjector.Applicability(note, new GameSettingsContext(Serial, RuntimeBuild: Exact("0.0.24", 12000))) == WikiNoteApplicability.OtherBuild &&
               GameSettingsProjector.Applicability(note, new GameSettingsContext(Serial)) == WikiNoteApplicability.Unresolved,
            "Projection: a proven-newer build applies, a proven-older build is OtherBuild, an unknown build stays Unresolved.");

        var versionAsBuild = Note("Regression in RPCS3 versions after v0.0.21-13399.", null, versionCondition: "0.0.21");
        Assert(GameSettingsProjector.Applicability(versionAsBuild, new GameSettingsContext(Serial, RuntimeBuild: Exact("0.0.35", 17000))) == WikiNoteApplicability.General,
            "A version-like scope that is really an RPCS3 version is a build scope and compares as one.");

        // End to end through the use case: the identity the log probe produced reaches the projector typed.
        var page = new WikiPageEvidence("Synthetic", [Serial], [note], []);
        var catalog = WikiEvidenceCatalog.FromPages("test", "test", "test", [page]);
        var game = new Rpcs3GameDescriptor(Serial, "Synthetic", string.Empty, "01.00", null, null);
        var useCase = new WikiEvidenceUseCase();
        var identity = new Rpcs3BuildIdentity("0.0.35", 17000, "abcdef12", "Alpha", "master", "x64", Rpcs3BuildIdentityState.Exact, []);
        var model = useCase.Project(null, game, null, catalog, identity);
        Assert(model.Notes.Count == 1 && model.Notes[0].Applicability == WikiNoteApplicability.General && model.CurrentRpcs3Build == "0.0.35-17000",
            "The use case passes version, build and commit as typed facts (previously only VersionText, which could never satisfy a build scope).");
        var stale = identity with { State = Rpcs3BuildIdentityState.Conflict };
        Assert(useCase.Project(null, game, null, catalog, stale).Notes[0].Applicability == WikiNoteApplicability.Unresolved &&
               useCase.Project(null, game, null, catalog, null).Notes[0].Applicability == WikiNoteApplicability.Unresolved,
            "A conflicting or absent runtime identity leaves a build-scoped note Unresolved.");
    }
}
