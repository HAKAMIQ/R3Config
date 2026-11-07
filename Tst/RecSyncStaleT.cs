using System;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Vm;
using static R3Config.Tests.TestAssert;

namespace R3Config.Tests;

// The Wiki source row shows a Stale state with its date when the last
// successful sync is older than 30 days. Without any successful sync the date
// of the embedded data (BaselineVersion) is used. No new enum: the state is
// derived inside RecSyncVm from LastSuccessUtc.
internal static class RecSyncStaleT
{
    private static readonly DateTimeOffset Now = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);

    public static void Run()
    {
        var original = UiText.CurLang;
        try
        {
            foreach (var lang in new[] { UiText.EnLang, UiText.ArabLang })
            {
                UiText.SetLang(lang);
                Cases(lang);
            }

            Assert(UiText.GetText("Sync.Wiki.Stale") != "Sync.Wiki.Stale",
                "Sync.Wiki.Stale must exist in the language pack.");
        }
        finally
        {
            UiText.SetLang(original);
        }
    }

    private static void Cases(string lang)
    {
        var plain = UiText.GetText("Sync.Status.Unchanged");

        var recent = Now.AddDays(-29);
        Assert(WikiStatus(recent, "2020-01-01") == plain,
            lang + ": a sync 29 days ago is not stale (the old embedded data version is ignored).");

        Assert(WikiStatus(Now.AddDays(-30), "2020-01-01") == plain,
            lang + ": exactly 30 days is not stale.");

        var old = Now.AddDays(-31);
        var stale = WikiStatus(old, "2026-11-01");
        Assert(stale == plain + " | " + UiText.Fmt("Sync.Wiki.Stale", UiDate.Inline(old)),
            lang + ": a sync 31 days ago must show Stale with its date. Got: " + stale);

        // No successful sync: the embedded data version decides.
        var embedded = new DateTimeOffset(new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Local));
        var never = UiText.GetText("Sync.Status.NeverChecked");
        Assert(WikiStatus(null, "2026-09-20", RecSyncSourceStatus.NeverChecked) ==
               never + " | " + UiText.Fmt("Sync.Wiki.Stale", UiDate.Inline(embedded)),
            lang + ": without a successful sync, old embedded data is Stale with its date.");
        Assert(WikiStatus(null, "2026-10-20", RecSyncSourceStatus.NeverChecked) == never,
            lang + ": recent embedded data is not stale.");
        Assert(WikiStatus(null, "not-a-date", RecSyncSourceStatus.NeverChecked) == never,
            lang + ": an unparsable embedded version never invents a Stale state.");

        var vm = Load(old, "2026-11-01");
        foreach (var row in vm.Sources)
        {
            if (row.Name != UiText.GetText("Sync.Source.Wiki"))
            {
                Assert(!row.Status.Contains(UiText.Fmt("Sync.Wiki.Stale", UiDate.Inline(old)), StringComparison.Ordinal),
                    lang + ": only the Wiki row may be marked Stale.");
            }
        }
    }

    private static string WikiStatus(
        DateTimeOffset? lastSuccess,
        string baselineVersion,
        RecSyncSourceStatus status = RecSyncSourceStatus.Unchanged)
    {
        var vm = Load(lastSuccess, baselineVersion, status);
        return vm.Sources.Single(row => row.Name == UiText.GetText("Sync.Source.Wiki")).Status;
    }

    private static RecSyncVm Load(
        DateTimeOffset? lastSuccess,
        string baselineVersion,
        RecSyncSourceStatus status = RecSyncSourceStatus.Unchanged)
    {
        var data = RecSyncStoreData.Empty("HASH", baselineVersion) with
        {
            Sources =
            [
                new RecSyncSourceState(RecSyncSource.ConfigApi, RecSyncSourceStatus.NeverChecked, null, null, null, 0, null, null, null, null, null, null),
                new RecSyncSourceState(RecSyncSource.Wiki, status, lastSuccess, lastSuccess, null, 0, null, null, null, null, null, null),
                new RecSyncSourceState(RecSyncSource.GitHub, RecSyncSourceStatus.NeverChecked, null, null, null, 0, null, null, null, null, null, null)
            ]
        };
        var vm = new RecSyncVm { Now = () => Now };
        vm.Load(data, RecSyncStoreHealth.Fresh, 1, 1, null);
        return vm;
    }
}
