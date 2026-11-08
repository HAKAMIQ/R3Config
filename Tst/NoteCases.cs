using System;

namespace R3Config.Tests;

// Registration of the Wiki-note/Online/Stale focused cases. Program.cs only
// delegates here so the entry point stays under its size gate.
internal static class NoteCases
{
    public static bool TryRunFlag(TestRunner runner, string flag)
    {
        switch (flag.ToLowerInvariant())
        {
            case "--online-notes-only":
                runner.RunCase(nameof(OnlineNotesT), OnlineNotesT.Run);
                return true;
            case "--rec-sync-stale-only":
                runner.RunCase(nameof(RecSyncStaleT), RecSyncStaleT.Run);
                return true;
            case "--reviewed-notes-only":
                runner.RunCase(nameof(ReviewedNotesImportT), ReviewedNotesImportT.Run);
                return true;
            case "--wiki-label-link-only":
                runner.RunCase(nameof(WikiLabelLinkT), WikiLabelLinkT.Run);
                return true;
            case "--note-reveal-only":
                runner.RunCase(nameof(NoteRevealT), NoteRevealT.Run);
                return true;
            case "--note-bidi-only":
                runner.RunCase(nameof(NoteBidiT), NoteBidiT.Run);
                return true;
            case "--sync-bounds-only":
                runner.RunCase(nameof(SyncBoundsT), SyncBoundsT.Run);
                return true;
            default:
                return false;
        }
    }

    public static void RunAll(TestRunner runner)
    {
        runner.RunCase(nameof(OnlineNotesT), OnlineNotesT.Run);
        runner.RunCase(nameof(RecSyncStaleT), RecSyncStaleT.Run);
        runner.RunCase(nameof(ReviewedNotesImportT), ReviewedNotesImportT.Run);
        runner.RunCase(nameof(WikiLabelLinkT), WikiLabelLinkT.Run);
        runner.RunCase(nameof(SyncBoundsT), SyncBoundsT.Run);
        runner.RunCase(nameof(NoteRevealT), NoteRevealT.Run);
        runner.RunCase(nameof(NoteBidiT), NoteBidiT.Run);
    }
}
