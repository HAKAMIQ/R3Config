using System;
using System.Linq;
using R3Config.Application;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;
using R3Config.Vm;

// RPCS3 writes one log for the last session, not one log per game. The panel
// must therefore state whose session it analysed in every case, so a session
// that booted another game is never read as the selected game's own log.
internal static class LogDiagSessionOwnerT
{
    public static void Run()
    {
        var previous = UiText.CurLang;
        try
        {
            foreach (var lang in new[] { UiText.ArabLang, UiText.EnLang })
            {
                UiText.SetLang(lang);
                AssertOtherGameWithProblemsIsStated(lang);
                AssertOwnerIsStatedForEveryCase(lang);
                AssertIdentityNamesTheSessionNotTheSelection(lang);
            }
        }
        finally
        {
            UiText.SetLang(previous);
        }

        Console.WriteLine("Log Diagnostics session ownership (other game, no boot, selected game) PASS.");
    }

    // The regression: an other-game session that HAS problems fell through the
    // !HasProblems branch, so nothing on screen said the events belong to a
    // different game. Ownership must be decided before that split.
    private static void AssertOtherGameWithProblemsIsStated(string lang)
    {
        var vm = Vm(serial: "BLES00001", notes: ["LogFromOtherGame"], withProblem: true);

        Assert(vm.HasProblems,
            $"{lang}: the fixture must carry a problem event, otherwise it cannot reproduce the regression.");
        Assert(vm.HasStateText && vm.StateText.Length > 0,
            $"{lang}: an other-game session with problems must still state whose session it is, not leave the state line empty.");
        Assert(vm.StateText.Contains("BLES00001", StringComparison.Ordinal),
            $"{lang}: the state line must name the serial the analysed session actually booted.");
    }

    // The card declares ownership in all three cases, not only on a mismatch.
    private static void AssertOwnerIsStatedForEveryCase(string lang)
    {
        var own = Vm(serial: "BLUS31465", notes: [], withProblem: false);
        var other = Vm(serial: "BLES00001", notes: ["LogFromOtherGame"], withProblem: true);
        var noBoot = Vm(serial: null, notes: ["LogHasNoGameBoot"], withProblem: false);

        Assert(own.SessionOwner == LogSessionOwner.SelectedGame &&
               other.SessionOwner == LogSessionOwner.OtherGame &&
               noBoot.SessionOwner == LogSessionOwner.NoGameBoot,
            $"{lang}: ownership must be derived for the selected game, another game and a session with no game boot.");

        foreach (var (name, vm) in new[] { ("selected game", own), ("other game", other), ("no game boot", noBoot) })
        {
            Assert(vm.HasSessionIdentity && vm.SessionOwnerText.Length > 0,
                $"{lang}: the {name} case must state its ownership instead of leaving the chip empty.");
            Assert(vm.SessionScopeText.Length > 0 && vm.SessionTitle.Length > 0,
                $"{lang}: the {name} case must keep the session title and the one-log-per-session scope line.");
        }

        Assert(own.IsSessionOwnedBySelectedGame && !other.IsSessionOwnedBySelectedGame && !noBoot.IsSessionOwnedBySelectedGame,
            $"{lang}: only a session that booted the selected game may be presented as that game's own.");
        Assert(other.SessionOwnerText != own.SessionOwnerText && noBoot.SessionOwnerText != own.SessionOwnerText,
            $"{lang}: the three ownership cases must not share one wording.");

        // The card owns the ownership message; the notes line must not repeat it.
        foreach (var (name, vm) in new[] { ("other game", other), ("no game boot", noBoot) })
        {
            Assert(!vm.HasNotes && vm.NotesText.Length == 0,
                $"{lang}: the {name} case must not also repeat its ownership in the notes line: '{vm.NotesText}'.");
        }

        var kept = Vm(serial: "BLUS31465", notes: ["LogFromOtherGame", "Truncated"], withProblem: false);
        Assert(kept.HasNotes && kept.NotesText.Length > 0 &&
               !kept.NotesText.Contains(UiText.GetText("Diag.Note.LogFromOtherGame"), StringComparison.Ordinal),
            $"{lang}: unrelated notes must survive while only the ownership note is dropped: '{kept.NotesText}'.");

        var notRun = new LogDiagVm();
        Assert(notRun.SessionOwner == LogSessionOwner.Unknown && !notRun.HasSessionIdentity,
            $"{lang}: before an analysis there is no session, so no ownership may be claimed.");
    }

    // The identity line is the analysed session's own subject.
    private static void AssertIdentityNamesTheSessionNotTheSelection(string lang)
    {
        var other = Vm(serial: "BLES00001", notes: ["LogFromOtherGame"], withProblem: true);
        Assert(other.SessionIdentityText.Contains("BLES00001", StringComparison.Ordinal) &&
               other.SessionIdentityText.Contains("01.05", StringComparison.Ordinal),
            $"{lang}: the identity line must carry the serial and version the session booted.");

        var noBoot = Vm(serial: null, notes: ["LogHasNoGameBoot"], withProblem: false);
        Assert(!noBoot.HasSessionSerial && noBoot.SessionIdentityText.Length == 0 &&
               noBoot.SessionOwnerText.Length > 0,
            $"{lang}: a session with no game boot must hide the technical identity line and state the case in the ownership line instead.");
        Assert(other.HasSessionSerial,
            $"{lang}: a session that booted a game must show its technical identity line.");
    }

    // ------------------------------------------------------------- fixtures

    private static LogDiagVm Vm(string? serial, string[] notes, bool withProblem)
    {
        var events = withProblem
            ? new[] { Problem("SIG", "Thread terminated due to fatal error: Failed to compile fragment shader") }
            : [];
        var session = LogSession.Empty with
        {
            Build = new Rpcs3BuildBanner("0.0.42", 19900, "abcdef12", "Alpha", "master", "x64"),
            GameSerial = serial,
            AppVersion = "01.05",
            Events = events
        };
        var report = new DiagnosticReport(session, [], notes);
        var vm = new LogDiagVm();
        vm.Load(new LogDiagnosticsResult(report, [session], notes, null));
        return vm;
    }

    private static LogEvent Problem(string channel, string message, char level = 'F') =>
        new(10, 10, 1, level, null, channel, message,
            [.. LogRules.Classify(level, channel, message)],
            LogRules.BuildSignature(level, channel, message));
}
