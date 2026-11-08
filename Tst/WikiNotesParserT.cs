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
            (nameof(VersionIpv4), VersionIpv4),
            (nameof(VersionRpcs3Build), VersionRpcs3Build),
            (nameof(BuildScopeRpcs3Build), BuildScopeRpcs3Build),
            (nameof(StruckNotes), StruckNotes),
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

    // Round 13: a dotted number inside an IPv4 address (bare or host=IPv4) is
    // never a game version, even when a version word such as "below" occurs
    // elsewhere in the note. The two IP/Host sections are copied verbatim from
    // the pinned snapshot RPCS3+Wiki-20260920160231.xml (Demon's Souls page id 4
    // revision 58715; The Lord of the Rings Conquest page id 2619 revision 57670).
    public static void VersionIpv4()
    {
        var demonsSouls = WikiNotesParser.Parse(
            "Demon's Souls",
            ["BLUS30443"],
            """
            ==Special Notes==
            ===Netplay IP/Host Switches===
            This title requires a private server to access all Netplay features. Enter the below text in the '''IP/Host Switches''' section:
            <pre>ds-eu-c.scej-online.jp=206.189.232.242&&ds-eu-g.scej-online.jp=206.189.232.242&&c.demons-souls.com=206.189.232.242&&g.demons-souls.com=206.189.232.242&&cmnap.scej-online.jp=206.189.232.242&&demons-souls.scej-online.jp=206.189.232.242</pre>
            [[Category:Custom servers (Netplay)]]
            """);
        var demonsNote = demonsSouls.Notes.Single(note => note.Text.Contains("206.189.232.242", StringComparison.Ordinal));
        Assert(demonsNote.VersionCondition is null,
            $"Demon's Souls IP/Host note must not take a VersionCondition from its IPv4 address, got '{demonsNote.VersionCondition}'.");

        var conquest = WikiNotesParser.Parse(
            "The Lord of the Rings Conquest",
            ["BLUS30226"],
            """
            ==Special Notes==
            ===Arcadia===
            Enter the below text in the '''IP/Host Switches''' section:
            <pre>theater.ps3.arcadia=152.53.15.83&&lotr-pandemic-ps3.fesl.ea.com=152.53.15.83</pre>
            """);
        var conquestNote = conquest.Notes.Single(note => note.Text.Contains("152.53.15.83", StringComparison.Ordinal));
        Assert(conquestNote.VersionCondition is null,
            $"LotR Conquest IP/Host note must not take a VersionCondition from its IPv4 address, got '{conquestNote.VersionCondition}'.");

        Assert(VersionOf("Set the primary DNS to 185.194.142.4 after connecting to RPCN.") is null,
            "An independent IPv4 address must not become a VersionCondition.");
        Assert(VersionOf("Enter the below text in the DNS section: nemo.example.net=203.0.113.5") is null,
            "A host=IPv4 switch must not become a VersionCondition.");

        Assert(VersionOf("The game runs well on 01.00, but crashes after the first boss.") == "01.00",
            "Explicit game version 01.00 must remain the VersionCondition.");
        Assert(VersionOf("Updating the game version to 1.03 may rectify this.") == "1.03",
            "Explicit game version 1.03 must remain the VersionCondition.");

        Assert(VersionOf("Enter the below text: a.example.net=203.0.113.5 and update the game to version 1.05.") == "1.05",
            "A version stated after an IP must be chosen over the IP.");
        Assert(VersionOf("Update the game to version 1.05, then enter the below text: a.example.net=203.0.113.5") == "1.05",
            "A version stated before an IP must be chosen over the IP.");

        var build = Single("The game requires a uDraw GameTablet USB peripheral to be connected and configured for passthrough to RPCS3. Support for this was added in RPCS3 version 0.0.13-11475.");
        Assert(build.Rpcs3BuildCondition == "0.0.13-11475" && build.VersionCondition == "0.0.13",
            $"RPCS3 build wording must stay build scope (0.0.13-11475 / 0.0.13), got '{build.Rpcs3BuildCondition}' / '{build.VersionCondition}'.");
    }

    // Round 14: a dotted number inside RPCS3 build wording (v0.0.N-NNNN or
    // rpcs3-v0.0.N) is never a game version. The Catherine sentence and the
    // Madden NFL 10 section are copied verbatim (entities decoded) from the
    // pinned snapshot RPCS3+Wiki-20260920160231.xml (Catherine page id 51
    // revision 58286; Madden NFL 10 page id 1817 revision 52837). The wiki
    // wraps the Catherine sentence in <s>, and a wholly struck note is dropped
    // since round 15 (StruckNotes), so the sentence is used here without it.
    // Round 15: the same wording is read as build scope (BuildScopeRpcs3Build).
    public static void VersionRpcs3Build()
    {
        var catherineNote = Single("For certain CPUs, the game hangs on a black screen after the Atlus logo. This issue was first visible after {{pr|4422}} and is being tracked as part of {{issue|4473}}. For people affected by this issue, use this [https://obk.ee/rpcs3/archive/rpcs3-v0.0.5-6664-47752221_win64.7z older build] of the emulator.");
        Assert(catherineNote.VersionCondition is null,
            $"Catherine note must not take a VersionCondition from 'rpcs3-v0.0.5-6664', got '{catherineNote.VersionCondition}'.");
        Assert(catherineNote.Rpcs3BuildCondition == "0.0.5-6664",
            $"rpcs3-v0.0.5-6664 must be build scope 0.0.5-6664, got '{catherineNote.Rpcs3BuildCondition}'.");

        var madden = WikiNotesParser.Parse(
            "Madden NFL 10",
            ["BLUS30375"],
            """
            ==Known Issues==
            <!-- Mention any known issues relating to the game. Use the issue and PR template if necessary. If no notable issues are present, mention "There are no reported issues with this title." -->
            ===Black lines across menus===
            * {{issue|12514}}: Black lines appear across the title screen and game menus in RPCS3 versions after v0.0.21-13399.
            """);
        var maddenNote = madden.Notes.Single(note => note.Text.Contains("v0.0.21-13399", StringComparison.Ordinal));
        Assert(maddenNote.VersionCondition is null,
            $"Madden NFL note must not take a VersionCondition from 'v0.0.21-13399', got '{maddenNote.VersionCondition}'.");
        Assert(maddenNote.Rpcs3BuildCondition == "0.0.21-13399",
            $"v0.0.21-13399 must be build scope 0.0.21-13399, got '{maddenNote.Rpcs3BuildCondition}'.");

        Assert(VersionOf("Crashes on newer builds, so use the rpcs3-v0.0.5 build or older.") is null,
            "rpcs3-v0.0.N without a build number must not become a VersionCondition.");
        Assert(VersionOf("Update the game to version 1.05; menus break in RPCS3 versions after v0.0.21-13399.") == "1.05",
            "A game version stated before RPCS3 build wording must be chosen over the build.");
        Assert(VersionOf("Menus break in RPCS3 versions after v0.0.21-13399 unless the game is updated to 1.05.") == "1.05",
            "A game version stated after RPCS3 build wording must be chosen over the build.");
        Assert(VersionOf("If you get a crash with version 01.11, set the network status as disabled.") == "01.11",
            "Explicit game version 01.11 must remain the VersionCondition.");

        var added = Single("The game requires a 'Ride Gate'/'Summonride Portal' USB peripheral to be connected and configured for passthrough to RPCS3. Support for this was added in RPCS3 version 0.0.17-12652.");
        Assert(added.Rpcs3BuildCondition == "0.0.17-12652" && added.VersionCondition == "0.0.17",
            $"Bare RPCS3 build wording must stay build scope (0.0.17-12652 / 0.0.17), got '{added.Rpcs3BuildCondition}' / '{added.VersionCondition}'.");
    }

    // Round 15: Rpcs3BuildNumber also reads RPCS3 build wording (v0.0.N-NNNN or
    // rpcs3-v0.0.N) as build scope, and never reads a number inside an IPv4
    // address. Wiki lines are verbatim (entities decoded) from the pinned
    // snapshot RPCS3+Wiki-20260920160231.xml: All-Pro Football 2K8 page id 765
    // revision 58005; Ace Combat Infinity page id 1259 revision 58886.
    public static void BuildScopeRpcs3Build()
    {
        Assert(BuildOf("{{issue|12514}}: Black lines appear across the title screen and game menus in RPCS3 versions after v0.0.21-13399.") == "0.0.21-13399",
            "v0.0.21-13399 must be build scope 0.0.21-13399.");
        Assert(BuildOf("{{issue|13253}}: There are graphical glitches (flimmering on helmets + pixelated banners) since RPCS v0.0.26-14569 when both WCB and CPU blit are enabled.") == "0.0.26-14569",
            "v0.0.26-14569 must be build scope 0.0.26-14569.");
        Assert(BuildOf("For people affected by this issue, use this [https://obk.ee/rpcs3/archive/rpcs3-v0.0.5-6664-47752221_win64.7z older build] of the emulator.") == "0.0.5-6664",
            "rpcs3-v0.0.5-6664 must be build scope 0.0.5-6664.");
        Assert(BuildOf("Crashes on newer builds, so use the rpcs3-v0.0.5 build or older.") == "0.0.5",
            "rpcs3-v0.0.5 must be build scope 0.0.5.");

        var aceCombat = WikiNotesParser.Parse(
            "Ace Combat Infinity",
            ["NPUB31347"],
            """
            ==Special Notes==
            ===Netplay IP/Host Switches===
            This title requires a private server to access all Netplay features. Enter the below text in the '''IP/Host Switches''' section:
            ====Local server====
            <pre>dev-wind.siliconstudio.co.jp=127.0.0.1</pre>
            """);
        var local = aceCombat.Notes.Single(note => note.Text.Contains("127.0.0.1", StringComparison.Ordinal));
        Assert(local.Rpcs3BuildCondition is null && local.VersionCondition is null,
            $"An IPv4 address must not become build scope, got '{local.Rpcs3BuildCondition}' / '{local.VersionCondition}'.");
        Assert(BuildOf("Enter the below text: a.example.net=127.0.0.1. Broken since RPCS3 0.0.17-12650.") == "0.0.17-12650",
            "A build stated after an IPv4 address must be chosen over the address.");

        foreach (var (text, build, version) in new[]
                 {
                     ("Support for this was added in RPCS3 version 0.0.13-11475.", "0.0.13-11475", "0.0.13"),
                     ("Support for this was added in RPCS3 version 0.0.17-12652.", "0.0.17-12652", "0.0.17"),
                     ("RPCS3 issue #11122: Excessive amounts of bloom occur in versions of RPCS3 since 0.0.17-12650", "0.0.17-12650", null),
                     ("The game, in its current state (build 0.0.24-14141), will have difficulty maintaining 60fps.", "0.0.24-14141", null)
                 })
        {
            var note = Single(text);
            Assert(note.Rpcs3BuildCondition == build && note.VersionCondition == version,
                $"Bare RPCS3 build wording must stay unchanged ({build} / {version ?? "null"}), got '{note.Rpcs3BuildCondition}' / '{note.VersionCondition}'.");
        }

        Assert(BuildOf("The game will freeze during the loading screen if its not updated to version 1.09.") is null &&
               VersionOf("The game will freeze during the loading screen if its not updated to version 1.09.") == "1.09",
            "A game version must stay a game version and never become build scope.");
    }

    // Round 15: a note that the Wiki has wholly struck through (<s>...</s>) is
    // no longer evidence and is not emitted; a partly struck note is kept as is.
    // Sections are verbatim (entities decoded) from the pinned snapshot
    // RPCS3+Wiki-20260920160231.xml: Catherine page id 51 revision 58286;
    // Madden NFL 12 page id 1819 revision 55629; Rampart page id 2172 revision
    // 57249; Fuel Overdose page id 1587 revision 54678.
    public static void StruckNotes()
    {
        var catherine = WikiNotesParser.Parse(
            "Catherine",
            ["BLUS30428"],
            """
            ==Known Issues==
            <!-- Mention any known issues relating to the game. Use the issue and PR template if necessary. If no notable issues are present, mention "There are no reported issues with this title." -->
            There are no reported issues with this title.
            ===Black Screen after Altus logo - <i>Fixed by {{pr|5310}}</i>===
            <s>For certain CPUs, the game hangs on a black screen after the Atlus logo. This issue was first visible after {{pr|4422}} and is being tracked as part of {{issue|4473}}. For people affected by this issue, use this [https://obk.ee/rpcs3/archive/rpcs3-v0.0.5-6664-47752221_win64.7z older build] of the emulator.</s>
            """);
        Assert(catherine.Notes.Count == 1 && catherine.Notes[0].Text == "There are no reported issues with this title.",
            $"Catherine's wholly struck note must not be emitted, got: {string.Join(" | ", catherine.Notes.Select(note => note.Text))}.");

        var madden12 = WikiNotesParser.Parse(
            "Madden NFL 12",
            ["BLUS30770"],
            """
            ==Known Issues==
            <!-- Mention any known issues relating to the game. Use the issue and PR template if necessary. If no notable issues are present, mention "There are no reported issues with this title." -->

            ===<s>Black lines across menus</s>===
            <s>* {{issue|12514}}: Black lines appear across the title screen and game menus in RPCS3 versions after v0.0.21-13399.</s><!--Fixed by PR 14131 in July 2023-->
            """);
        Assert(madden12.Notes.Count == 0,
            $"Madden NFL 12's wholly struck note must not be emitted, got: {string.Join(" | ", madden12.Notes.Select(note => note.Text))}.");

        var rampart = WikiNotesParser.Parse(
            "Rampart",
            [],
            """
            ==Known Issues==
            ===Crashing at Main Menu===
            *<s>{{issue|14860}}: The game freezes at the main menu</s> - Resolved by PR 15167
            """);
        Assert(rampart.Notes.Count == 1 && rampart.Notes[0].Text == "RPCS3 issue #14860: The game freezes at the main menu - Resolved by PR 15167",
            $"A partly struck note must be kept unchanged, got: {string.Join(" | ", rampart.Notes.Select(note => note.Text))}.");

        var fuel = WikiNotesParser.Parse(
            "Fuel Overdose",
            [],
            """
            ==Known Issues==
            There are no reported issues with this title.

            ===<s>Disk space error</s>===
            *<s>{{issue|13301}} - The demo version and full game prior to version 1.05 can't make it past the intro FMV's due to an error calculating available disk space to create the autosave data.</s> - Resolved by PR 15313
            """);
        var fuelNote = fuel.Notes.SingleOrDefault(note => note.Text.Contains("13301", StringComparison.Ordinal));
        Assert(fuel.Notes.Count == 2 && fuelNote is not null && fuelNote.VersionCondition == "1.05",
            $"A partly struck note must be kept with its VersionCondition 1.05, got: {string.Join(" | ", fuel.Notes.Select(note => $"{note.Text} [{note.VersionCondition}]"))}.");

        Assert(Single("<s>An unclosed strike tag keeps the note.").Text == "An unclosed strike tag keeps the note.",
            "A note whose strike tags do not balance must be kept.");
    }

    private static string? BuildOf(string bullet) => Single(bullet).Rpcs3BuildCondition;

    private static string? VersionOf(string bullet) => Single(bullet).VersionCondition;

    private static WikiFreeTextNote Single(string bullet)
    {
        var evidence = WikiNotesParser.Parse("VersionPage", [], "==Special Notes==\n* " + bullet);
        Assert(evidence.Notes.Count == 1,
            $"Expected one note, got {evidence.Notes.Count}.");
        return evidence.Notes[0];
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
