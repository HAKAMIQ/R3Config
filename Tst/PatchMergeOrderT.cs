using System.Linq;
using R3Config.Mdl;
using R3Config.Svc;
using static R3Config.Tests.TestAssert;

namespace R3Config.Tests;

internal static class PatchMergeOrderT
{
    private static Rpcs3PatchEntry Cheat(PatSrc source, string? version, string definition) =>
        Entry(version, definition) with
        {
            Title = "Resident Probe",
            Risk = PatRisk.Cheat,
            Source = source,
            SrcPath = source == PatSrc.ManagedArtemis ? "BLUS12345_patch.yml" : "art.yml"
        };

    private static Rpcs3PatchEntry Entry(string? version, string? definition) => new(
        Title: "Order Probe",
        HashKind: HashKind.Ppu,
        Hash: "1111111111111111111111111111111111111111",
        GameTgt: [new R3PatTgt("Probe", "BLUS12345", ["01.00"])],
        PatType: "Fix",
        Risk: PatRisk.SafeFix,
        Notes: null,
        Author: "R3Config",
        PatVer: version,
        Group: null,
        Source: PatSrc.Imported,
        SrcPath: "imported_patch.yml",
        DefPrint: definition);

    // Changed content whose version order cannot be proven (missing or
    // RPCS3-incomparable PatVer) must not be settled by load order or by
    // treating a missing version as older.
    public static void Run()
    {
        var merge = new PatchCatalogMergeService();
        foreach (var other in new string?[] { "9.0-beta", "v9.0", "", null })
        {
            var known = Entry("1.2", "aaaa");
            var unproven = Entry(other, "bbbb");
            foreach (var (first, second) in new[] { (known, unproven), (unproven, known) })
            {
                var result = merge.Merge(
                    new PatCat("1.2", [first]),
                    new PatCat("1.2", [second]));
                Assert(result.Entries.Count == 0 &&
                       result.Diag.Count == 1 &&
                       result.Diag.Single().Code == "PATCH_DUPLICATE_CONFLICT",
                    $"Expected changed content with unprovable version order ('{other}') to be rejected independent of load order.");
            }
        }

        // Provable version order is deterministic: the newer definition wins in either load order.
        var older = Entry("1.1", "aaaa");
        var newer = Entry("1.2", "bbbb");
        foreach (var (first, second) in new[] { (older, newer), (newer, older) })
        {
            var result = merge.Merge(new PatCat("1.2", [first]), new PatCat("1.2", [second]));
            Assert(result.Entries.Count == 1 && result.Entries.Single().PatVer == "1.2" && result.Diag.All(item => item.Code != "PATCH_DUPLICATE_CONFLICT"),
                "Provably ordered versions must resolve to the newer definition in either load order.");
        }

        // An updated bundled art.yml can change a definition the user already
        // installed into the managed <SERIAL>_patch.yml at an equal or
        // unordered Patch Version. The resident copy is what RPCS3 applies, so
        // it must stay visible (and therefore disable/removable) in either load
        // order instead of both copies being rejected.
        foreach (var bundledVersion in new string?[] { "1.2", "9.0-beta", null })
        {
            var resident = Cheat(PatSrc.ManagedArtemis, "1.2", "old-bytes");
            var bundled = Cheat(PatSrc.Artemis, bundledVersion, "new-bytes");
            foreach (var (first, second) in new[] { (resident, bundled), (bundled, resident) })
            {
                var result = merge.Merge(new PatCat("1.2", [first]), new PatCat("1.2", [second]));
                Assert(result.Entries.Count == 1 &&
                       result.Entries.Single() == resident with { BundledChanged = true } &&
                       result.Diag.Count == 1 &&
                       result.Diag.Single().Code == "PATCH_MANAGED_RESIDENT_KEPT" &&
                       result.Diag.Single().KeepSrc == PatSrc.ManagedArtemis &&
                       result.Diag.Single().DropSrc == PatSrc.Artemis,
                    $"Expected the managed resident Artemis cheat to stay visible over a changed bundled definition ('{bundledVersion}') in either load order.");
            }
        }

        // The resident exception is limited to the ManagedArtemis/Artemis pair:
        // two changed bundled copies of one identity still fail closed.
        foreach (var source in new[] { PatSrc.Artemis, PatSrc.ManagedArtemis })
        {
            var result = merge.Merge(
                new PatCat("1.2", [Cheat(source, "1.2", "old-bytes")]),
                new PatCat("1.2", [Cheat(source, "1.2", "new-bytes")]));
            Assert(result.Entries.Count == 0 &&
                   result.Diag.Single().Code == "PATCH_DUPLICATE_CONFLICT",
                $"Expected two changed {source} copies of one identity to remain a rejected conflict.");
        }

        // A provably newer bundled version does not replace the resident copy
        // either; this was already the merge order and must not regress.
        foreach (var (first, second) in new[]
                 {
                     (Cheat(PatSrc.ManagedArtemis, "1.2", "old-bytes"), Cheat(PatSrc.Artemis, "1.5", "new-bytes")),
                     (Cheat(PatSrc.Artemis, "1.5", "new-bytes"), Cheat(PatSrc.ManagedArtemis, "1.2", "old-bytes"))
                 })
        {
            var result = merge.Merge(new PatCat("1.2", [first]), new PatCat("1.2", [second]));
            Assert(result.Entries.Count == 1 &&
                   result.Entries.Single().Source == PatSrc.ManagedArtemis &&
                   result.Entries.Single().PatVer == "1.2" &&
                   result.Entries.Single().BundledChanged,
                "Expected a resident managed Artemis cheat to stay selected over a newer bundled version and be flagged as changed.");
        }

        // An unchanged resident is not flagged.
        foreach (var (first, second) in new[]
                 {
                     (Cheat(PatSrc.ManagedArtemis, "1.2", "same-bytes"), Cheat(PatSrc.Artemis, "1.2", "same-bytes")),
                     (Cheat(PatSrc.Artemis, "1.2", "same-bytes"), Cheat(PatSrc.ManagedArtemis, "1.2", "same-bytes"))
                 })
        {
            var result = merge.Merge(new PatCat("1.2", [first]), new PatCat("1.2", [second]));
            Assert(result.Entries.Count == 1 &&
                   result.Entries.Single().Source == PatSrc.ManagedArtemis &&
                   !result.Entries.Single().BundledChanged &&
                   result.Diag.All(item => item.Code != "PATCH_MANAGED_RESIDENT_KEPT"),
                "Expected an unchanged resident managed Artemis cheat to stay unflagged.");
        }
    }
}
