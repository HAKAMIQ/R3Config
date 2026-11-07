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
            default:
                return false;
        }
    }

    public static void RunAll(TestRunner runner)
    {
        runner.RunCase(nameof(OnlineNotesT), OnlineNotesT.Run);
        runner.RunCase(nameof(RecSyncStaleT), RecSyncStaleT.Run);
        runner.RunCase(nameof(ReviewedNotesImportT), ReviewedNotesImportT.Run);
    }
}
