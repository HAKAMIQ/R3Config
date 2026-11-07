using System;
using System.Linq;
using R3Config.Mdl;
using R3Config.Svc;

// Regression coverage for WikiNotesParser (free-text Wiki notes/warnings and
// the {{netplay}} online/RPCN evidence table). The fixture below is the real
// "Minecraft" page body from the pinned September Wiki snapshot
// (RPCS3+Wiki-20260907205217.xml, page id 1905), not an invented example, as
// required for this regression. It is embedded rather than read from disk so
// this test has no dependency on any external Wiki mirror path.
internal static class WikiNotesParserT
{
    internal const string MinecraftPage =
        """
        ==Known Issues==
        ===Split-screen Crash on RPCN - <i>{{issue|15055}}</i>===
        When attempting to create/join a world or minigame lobby via Split-screen while connected to RPCN, the game will crash. To prevent this, disconnect other player's controllers and load into the world. Afterwards, connect the other controllers. Must be done for every world/lobby you join.

        ==Special Notes==
        * Menus in this game are tied to the framerate. If the framerate is above or below 60FPS, certain menu elements like the Change Skin menu and the Interface cursor will be affected.

        * It is best you do not change the frame-limiter while the game is running, as doing so can cause the game to freeze.

        * The original digital 1.00 version released with a bug where the game's music is incorrectly pitched and sped up. Keep this in mind if you are interested in playing on 1.00.
        ===Note about MacOS===
        If you're on MacOS don't update the game past 1.00. The game will freeze on the menu screen when trying to start a new game if updated.

        {{netplay
        |gamever        = 1.84
        |rpcs3 version  = 0.0.30-16004
        |connect        = Yes<!-- Yes, No, Partial, Untested, N/A -->
        |lobbies        = Yes<!-- Yes, No, Partial, Untested, N/A -->
        |match          = Yes<!-- Yes, No, Partial, Untested, N/A -->
        |features       = Yes<!-- Yes, No, Partial, Untested, N/A -->
        |custom servers = Not required<!-- Available, Not available, Not required, Untested -->
        |notes          = RPCN 1.0 - 2024/01/14. **If you are crashing or freezing when trying to go to "Play Game" or "Mini Games", you must pre-load all the skins in the "Help & Options" menu, video demonstration.** https://i.imgur.com/wJWi7Au.mp4, you can also try clearing the cache by right-clicking the game, going to remove and then "Remove All Cache" while still doing the preloading step.
        }}
        """;

    public static void Run()
    {
        var evidence = WikiNotesParser.Parse(
            "Minecraft",
            ["BLES01976", "BLUS31426", "NPEB01899", "NPUB31419", "NPJB00549"],
            MinecraftPage);

        Assert(evidence.Notes.Count == 5,
            $"Expected exactly 5 free-text notes (1 Known Issues + 3 Special Notes bullets + 1 MacOS subsection), got {evidence.Notes.Count}.");

        var rpcnNote = evidence.Notes.Single(note => note.SourceSection.Contains("Split-screen", StringComparison.Ordinal));
        Assert(rpcnNote.Class == WikiNoteClass.Workaround,
            $"Expected the RPCN split-screen crash note to classify as Workaround (it states a concrete disconnect/reconnect fix), got {rpcnNote.Class}.");
        Assert(rpcnNote.Text.Contains("RPCN", StringComparison.Ordinal) &&
               rpcnNote.Text.Contains("crash", StringComparison.OrdinalIgnoreCase),
            "Expected the RPCN workaround text to be preserved verbatim, not summarized into a setting.");

        var framerateMenuNote = evidence.Notes.Single(note => note.Text.Contains("Change Skin menu", StringComparison.Ordinal));
        Assert(framerateMenuNote.Class == WikiNoteClass.InformationalNote,
            $"Expected the framerate/menu tie-in to stay an informational/compatibility note, not become 'Frame Limit = 60', got {framerateMenuNote.Class}.");

        var frameLimiterWarning = evidence.Notes.Single(note => note.Text.Contains("frame-limiter", StringComparison.Ordinal));
        Assert(frameLimiterWarning.Class == WikiNoteClass.Warning,
            $"Expected the 'do not change the frame-limiter while running' text to classify as a runtime Warning, not a setting, got {frameLimiterWarning.Class}.");

        var digitalMusicNote = evidence.Notes.Single(note => note.Text.Contains("pitched", StringComparison.Ordinal));
        Assert(digitalMusicNote.Class == WikiNoteClass.VersionSpecificNote &&
               digitalMusicNote.VersionCondition == "1.00",
            $"Expected the digital 1.00 music-bug note to be VERSION_SPECIFIC_NOTE with VersionCondition=1.00, got {digitalMusicNote.Class}/{digitalMusicNote.VersionCondition}.");

        var macNote = evidence.Notes.Single(note => note.SourceSection == "Note about MacOS");
        Assert(macNote.Class == WikiNoteClass.PlatformConditionalNote &&
               macNote.PlatformCondition == "MacOS" &&
               macNote.VersionCondition == "1.00",
            $"Expected the MacOS note to be PLATFORM_CONDITIONAL_NOTE scoped to MacOS and version 1.00, not a global recommendation, got {macNote.Class}/{macNote.PlatformCondition}/{macNote.VersionCondition}.");

        Assert(evidence.Online.Count == 1,
            $"Expected exactly one {{netplay}} table, got {evidence.Online.Count}.");
        var online = evidence.Online[0];
        Assert(online.State == WikiOnlineState.Supported,
            $"Expected Connect/Lobbies/Match/Features all Yes to resolve to Supported, got {online.State}.");
        Assert(online.TestedGameVersion == "1.84",
            $"Expected the netplay table's Game Version to be recorded as TESTED, not required, got '{online.TestedGameVersion}'.");
        Assert(online.RequiredGameVersion is null,
            "Expected no RequiredGameVersion: the Wiki's netplay table states a tested version, never a requirement, unless stated elsewhere.");
        Assert(online.Rpcs3VersionTested == "0.0.30-16004",
            $"Expected the RPCS3 build used for the netplay test to be preserved, got '{online.Rpcs3VersionTested}'.");
        Assert(online.RequiresCustomServers == "Not required",
            $"Expected the custom-server requirement to be preserved verbatim, got '{online.RequiresCustomServers}'.");
        Assert(online.Notes is not null && online.Notes.Contains("pre-load", StringComparison.OrdinalIgnoreCase),
            "Expected the RPCN skin/cache preload workaround note to be preserved.");

        // No online evidence at all (no {{netplay}} block on the page) must
        // resolve to UNKNOWN, never be interpreted as unsupported.
        var noEvidence = WikiNotesParser.Parse("NoNetplayPage", [], "Just a plain page with no netplay table.");
        Assert(noEvidence.Online.Count == 0,
            "Expected zero online evidence records when no {{netplay}} template exists on the page.");

        // A multi-line {{patch}} template after a notes section is patch data
        // (YAML), never prose: its body must not leak into notes.
        var withPatch = WikiNotesParser.Parse(
            "PatchBlockPage",
            [],
            """
            ==Special Notes==
            * Real note about the game.

            {{patch
            |type    = PPU
            |content =
            Anchors:
              game_100: &game_100
                "Game":
                  BLUS00000: [ 01.00 ]
            PPU-abc: "Skip Intro": Games: *game_100
            }}
            """);
        Assert(withPatch.Notes.Count == 1 && withPatch.Notes[0].Text == "Real note about the game.",
            "Multi-line {{patch}} template bodies must not become Wiki notes.");

        Console.WriteLine("Wiki free-text notes and RPCN/netplay evidence regression (real Minecraft fixture) PASS.");
    }

    public static void RunCases(R3Config.Tests.TestRunner runner, string? only)
    {
        (string Name, Action Body)[] cases =
        [
            (nameof(PrReference), PrReference),
            (nameof(BangCommentEnd), BangCommentEnd),
            (nameof(WikiResidueOnly), WikiResidueOnly),
            (nameof(WikiNotesParserT), Run)
        ];
        foreach (var (name, body) in cases)
        {
            if (only is null || only.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                runner.RunCase(name, body);
            }
        }
    }

    public static void PrReference()
    {
        var evidence = WikiNotesParser.Parse(
            "PrPage",
            [],
            "==Known Issues==\n* Fixed by {{pr|1234}} in a later build, see also {{issue|99}}.");
        Assert(evidence.Notes.Count == 1,
            $"Expected one note, got {evidence.Notes.Count}.");
        var text = evidence.Notes[0].Text;
        Assert(text.Contains("RPCS3 PR #1234", StringComparison.Ordinal) &&
               text.Contains("RPCS3 issue #99", StringComparison.Ordinal) &&
               !text.Contains("{{", StringComparison.Ordinal),
            $"Expected {{{{pr|N}}}} to be rendered like {{{{issue|N}}}}, got '{text}'.");
    }

    public static void BangCommentEnd()
    {
        var evidence = WikiNotesParser.Parse(
            "CommentPage",
            [],
            "==Known Issues==\n* Visible start.<!-- hidden words --!> Visible end.");
        Assert(evidence.Notes.Count == 1,
            $"Expected one note, got {evidence.Notes.Count}.");
        var text = evidence.Notes[0].Text;
        Assert(!text.Contains("hidden", StringComparison.Ordinal) &&
               !text.Contains("--!>", StringComparison.Ordinal) &&
               text.Contains("Visible start.", StringComparison.Ordinal) &&
               text.Contains("Visible end.", StringComparison.Ordinal),
            $"Expected a comment closed by --!> to be removed, got '{text}'.");
    }

    public static void WikiResidueOnly()
    {
        var evidence = WikiNotesParser.Parse(
            "ResiduePage",
            [],
            "==Known Issues==\n* [[File:shot.png|200px]]\n* 120px\n* [[Category:Games]]\n* Category:Games\n" +
            "* Enter the below text in the DNS section:\n* 192.168.1.10\n* nemo.example.net=203.0.113.5\n" +
            "* https://example.org/rpcn\n* RPCN servers can disconnect you.");
        var texts = evidence.Notes.Select(note => note.Text).ToArray();
        Assert(!texts.Any(text => text.Equals("200px", StringComparison.Ordinal) ||
                                  text.Equals("120px", StringComparison.Ordinal) ||
                                  text.StartsWith("Category:", StringComparison.OrdinalIgnoreCase)),
            $"Expected size-only image and Category residue to be dropped, got: {string.Join(" | ", texts)}.");
        foreach (var kept in new[]
                 {
                     "Enter the below text in the DNS section:",
                     "192.168.1.10",
                     "nemo.example.net=203.0.113.5",
                     "https://example.org/rpcn",
                     "RPCN servers can disconnect you."
                 })
        {
            Assert(texts.Contains(kept, StringComparer.Ordinal),
                $"Expected user-facing line '{kept}' to be kept, got: {string.Join(" | ", texts)}.");
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
