using System.Linq;
using R3Config.Mdl;
using R3Config.Svc;
using static R3Config.Tests.TestAssert;

namespace R3Config.Tests;

internal static class PatchMergeOrderT
{
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
    }
}
