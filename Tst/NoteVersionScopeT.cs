using System;
using R3Config.Mdl;
using R3Config.Svc;

// Game-version scope of Wiki notes: the operator may follow the number ("1.05 and below"),
// and a "version" that is really an IP address or another dotted number is not a game
// version and must not hide a note. Real corpus wording, synthetic contexts.
internal static class NoteVersionScopeT
{
    private const string Serial = "BLUS99003";

    public static void Run()
    {
        const string Below = "For use on game versions 1.05 and below. Enable CPU Blit Emulation.";
        Assert(NoteScope.VersionApplies(Below, "1.05", "1.00") == true &&
               NoteScope.VersionApplies(Below, "1.05", "1.05") == true &&
               NoteScope.VersionApplies(Below, "1.05", "1.06") == false &&
               NoteScope.VersionApplies(Below, "1.05", null) is null,
            "'1.05 and below' applies to 1.00 and 1.05 but not 1.06, and stays unknown without an installed version.");
        const string Up = "Requires the game to be updated to 1.20 or newer.";
        Assert(NoteScope.VersionApplies(Up, "1.20", "1.19") == false && NoteScope.VersionApplies(Up, "1.20", "1.20") == true && NoteScope.VersionApplies(Up, "1.20", "1.21") == true,
            "'1.20 or newer' applies from 1.20 upward.");
        Assert(NoteScope.VersionApplies("Broken since 1.02 and below is fine.", "1.02", "1.05") == true,
            "A leading operator wins over a trailing phrase.");
        Assert(NoteScope.VersionApplies("Version 1.00 has a crash.", "1.00", "1.00") == true && NoteScope.VersionApplies("Version 1.00 has a crash.", "1.00", "1.02") == false,
            "A bare version still means exactly that version.");

        Assert(NoteScope.IsGameVersion("1.05") && NoteScope.IsGameVersion("01.11") && NoteScope.IsGameVersion("1.0") &&
               !NoteScope.IsGameVersion("95.217.209") && !NoteScope.IsGameVersion("0.0.21") && !NoteScope.IsGameVersion("206.189.232") && !NoteScope.IsGameVersion(null),
            "Only major.minor is a game version; IP fragments and RPCS3 versions are not.");

        var ip = new WikiFreeTextNote("NHL 10", "Netplay", WikiNoteClass.NetworkNote,
            "Enter the below text in the IP/Hosts switches section: gosredirector.ea.com==95.217.209.57", "95.217.209", null);
        Assert(GameSettingsProjector.Applicability(ip, new GameSettingsContext(Serial, "01.00")) == WikiNoteApplicability.General &&
               GameSettingsProjector.Applicability(ip, new GameSettingsContext(Serial)) == WikiNoteApplicability.General,
            "An IP fragment parsed as a 'version' no longer hides the note, whatever the installed game version.");

        var real = new WikiFreeTextNote("Gran Turismo 6", "Known Issues", WikiNoteClass.VersionSpecificNote, Below, "1.05", null);
        Assert(GameSettingsProjector.Applicability(real, new GameSettingsContext(Serial, "1.00")) == WikiNoteApplicability.General &&
               GameSettingsProjector.Applicability(real, new GameSettingsContext(Serial, "1.06")) == WikiNoteApplicability.OtherVersion &&
               GameSettingsProjector.Applicability(real, new GameSettingsContext(Serial)) == WikiNoteApplicability.Unresolved,
            "A real game-version scope still restricts the note and fails closed when the version is unknown.");

        Console.WriteLine("Wiki note game-version scope (trailing operators, IP fragments are not versions, unknown stays unresolved) PASS.");
    }
}
