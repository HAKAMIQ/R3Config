using System.IO;
using System.Text;
using R3Config.Application;
using R3Config.Composition;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;
using R3Config.Vm;
using static R3Config.Tests.TestAssert;

namespace R3Config.Tests;

// A user who enabled an Artemis cheat before art.yml was updated keeps that
// definition in the managed <SERIAL>_patch.yml. When the updated library
// changes the same identity at the same Patch Version, the installed copy must
// stay visible, enable/disable/removable, and removing it must expose the
// updated bundled definition.
internal static class ArtemisCatalogUpdateT
{
    public static void Run(string tempRoot)
    {
        var bundledRead = new ReadSvc().Read(
            BundledCheatPatchService.GetBndl(AppContext.BaseDirectory),
            PatSrc.Artemis);
        Assert(bundledRead.IsOk,
            "Expected the shipped art.yml to load for the catalog update scenario: " + bundledRead.Error);

        var bundled = bundledRead.Catalog.Entries.First(entry =>
            entry.HashKind == HashKind.Ppu &&
            !string.IsNullOrWhiteSpace(entry.PatVer) &&
            entry.Patches.Count == 0 &&
            entry.GameTgt.Count == 1 &&
            entry.GameTgt[0].Versions.Count == 1 &&
            !PatVer.IsAll(entry.GameTgt[0].Versions[0]) &&
            entry.Title.All(ch => ch is not ('"' or '\\')) &&
            entry.GameTgt[0].Title.All(ch => ch is not ('"' or '\\')));
        var target = bundled.GameTgt[0];
        var version = target.Versions[0];

        var env = GameSetupT.CreateEnvironment(Path.Combine(tempRoot, "artemis-catalog-update"));
        var install = env.Install;
        var managedPath = BundledCheatPatchService.GetPath(install.PatPath, target.Serial);
        File.WriteAllText(
            managedPath,
            BundledCheatPatchService.ManMark + "\n" +
            "Version: 1.2\n" +
            $"PPU-{bundled.Hash}:\n" +
            $"  \"{bundled.Title}\":\n" +
            "    Games:\n" +
            $"      \"{target.Title}\":\n" +
            $"        {target.Serial}: [ {version} ]\n" +
            "    Author: R3Config previous library\n" +
            $"    Patch Version: {bundled.PatVer}\n" +
            "    Patch:\n" +
            "      - [ be32, 0x00010000, 0x60000000 ]\n",
            new UTF8Encoding(false));

        var flow = AppComposition.BuildRun(new Rpcs3MutationGuard(() => false)).Flow;
        var game = new Rpcs3GameDescriptor(
            target.Serial,
            target.Title,
            env.GamePath,
            version,
            bundled.Hash,
            null);

        // 1. The installed copy stays visible and is flagged, not rejected.
        var resident = Single(flow, install, bundled, PatSrc.ManagedArtemis);
        Assert(resident.BundledChanged &&
               !string.Equals(resident.DefPrint, bundled.DefPrint, StringComparison.OrdinalIgnoreCase),
            "Expected the installed Artemis cheat to stay visible and flagged when the updated library changed it.");

        var residentMatch = new PatchMatchService().Match(resident, [game]);
        Assert(residentMatch.Status == MatStat.Match,
            "Expected the installed Artemis cheat to match its exact game.");
        var originalLanguage = UiText.CurLang;
        try
        {
            foreach (var language in new[] { UiText.EnLang, UiText.ArabLang })
            {
                UiText.SetLang(language);
                Assert(new PatItVm(residentMatch).ArtemisTip.Contains(
                           UiText.GetText("Patch.Artemis.BundledChanged"),
                           StringComparison.Ordinal),
                    $"Expected the installed Artemis row to tell the user an updated version is available ({language}).");
            }
        }
        finally
        {
            UiText.SetLang(originalLanguage);
        }

        // 2. Enable and disable only toggle patch_config.yml; the installed
        //    bytes are never replaced by the bundled definition.
        var managedBefore = File.ReadAllBytes(managedPath);
        var change = new PatPlnr().Build3(residentMatch)!;
        flow.ApplyPat(
            flow.BuildPatchPlan(install, [residentMatch], [], expertMode: false),
            install);
        Assert(new CfgRead().ReadSnap(install.PatCfg).IsOn(change) &&
               File.ReadAllBytes(managedPath).SequenceEqual(managedBefore),
            "Expected enabling the installed Artemis cheat to toggle activation without rewriting its definition.");

        flow.OffPat(flow.OffPlan(install, [residentMatch]), install);
        Assert(!new CfgRead().ReadSnap(install.PatCfg).IsOn(change) &&
               File.ReadAllBytes(managedPath).SequenceEqual(managedBefore),
            "Expected disabling the installed Artemis cheat to keep its definition.");

        // 3. Removing the installed copy exposes the updated bundled
        //    definition, which is then the only row for that identity.
        flow.RemoveArtemis(
            flow.OffPlan(install, [residentMatch]),
            install,
            [residentMatch]);
        Assert(!File.Exists(managedPath),
            "Expected Remove to delete the managed file that held only the installed cheat.");

        var updated = Single(flow, install, bundled, PatSrc.Artemis);
        Assert(!updated.BundledChanged &&
               string.Equals(updated.DefPrint, bundled.DefPrint, StringComparison.OrdinalIgnoreCase),
            "Expected the updated bundled definition to replace the removed installed copy.");

        Console.WriteLine("Artemis catalog update keeps installed cheats manageable PASS.");
    }

    private static Rpcs3PatchEntry Single(
        AppFlow flow,
        Rpcs3InstallDescriptor install,
        Rpcs3PatchEntry identity,
        PatSrc expectedSource)
    {
        var load = flow.LoadPat(install);
        Assert(load.Cat is not null,
            "Expected the patch catalog to load: " + load.Error);
        var rows = load.Cat!.Entries
            .Where(entry =>
                entry.RunKey.Equals(identity.RunKey, StringComparison.OrdinalIgnoreCase) &&
                entry.Title.Equals(identity.Title, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert(rows.Length == 1 && rows[0].Source == expectedSource,
            $"Expected exactly one {expectedSource} row for '{identity.Title}', found {rows.Length}: " +
            string.Join(", ", rows.Select(row => row.Source)));
        return rows[0];
    }
}
