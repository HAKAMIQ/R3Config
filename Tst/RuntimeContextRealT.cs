using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using R3Config.Composition;
using R3Config.Mdl;
using R3Config.Svc;

// Read-only real-machine acceptance for Domain 5. It runs the production
// analysis (the same AppFlow path the application uses) against a real RPCS3
// installation and prints the resulting Runtime Context. It never mutates the
// installation: the config/patch files are fingerprinted before and after and
// the run fails if either changed.
//
//   R3Test.exe --runtime-context-real <rpcs3.exe> [serial]
internal static class RuntimeContextRealT
{
    public static void Run(string exePath, string? serial)
    {
        var fullExe = Path.GetFullPath(exePath);
        Assert(File.Exists(fullExe), $"RPCS3 executable not found: {fullExe}");

        var before = Fingerprint(Path.GetDirectoryName(fullExe)!);
        var flow = AppComposition.BuildRun().Flow;
        var catalog = flow.LoadCat(AppContext.BaseDirectory).Cat
            ?? throw new InvalidOperationException("The shipped settings catalog could not be loaded.");
        var wiki = flow.LoadWikiEvidence(AppContext.BaseDirectory).Catalog;
        var builds = flow.LoadRpcs3Builds(AppContext.BaseDirectory);
        var analysis = flow.DoAsync(fullExe, catalog).GetAwaiter().GetResult();

        Print("Hardware", flow.RuntimeContext(analysis.Hw, analysis.Install, analysis.BuildIdentity, null, null, builds), RuntimeFactGroup.Hardware);
        var machine = flow.RuntimeContext(analysis.Hw, analysis.Install, analysis.BuildIdentity, null, null, builds);
        Print("Installation", machine, RuntimeFactGroup.Installation);
        Print("Build", machine, RuntimeFactGroup.Build);
        Console.WriteLine($"RUNTIME_ADAPTERS={analysis.Hw.GpuAdapters.Count} " +
                          string.Join(" | ", analysis.Hw.GpuAdapters.Select(adapter =>
                              $"{adapter.Name} vendor={adapter.Vendor} pci={adapter.PciVendorId}:{adapter.PciDeviceId} primary={adapter.IsPrimary}")));
        // Only games actually installed in this RPCS3 (not catalog-only entries).
        var installed = analysis.Games.Where(item => item.InR3).ToArray();
        Console.WriteLine($"RUNTIME_GAMES_INSTALLED={installed.Length} CATALOG_ENTRIES={analysis.Games.Count} SERIALS={string.Join(",", installed.Take(12).Select(item => item.Game.Serial))}");

        var row = serial is null
            ? installed.FirstOrDefault(item => item.Rec is not null && wiki.Find(item.Game.Serial, item.Rec.WikiName) is not null) ??
              (installed.Length > 0 ? installed[0] : null)
            : installed.FirstOrDefault(item => string.Equals(item.Game.Serial, serial, StringComparison.OrdinalIgnoreCase));
        if (row is null)
        {
            Console.WriteLine("RUNTIME_GAME=NONE_SELECTED (no matching installed game)");
        }
        else
        {
            var plan = flow.CfgPlan(analysis.Install, row.Game, row.Rec, analysis.Hw);
            var model = flow.GameSettings(plan, row.Game, row.Rec, wiki, analysis.BuildIdentity, analysis.Hw, builds);
            var snapshot = flow.RuntimeContext(analysis.Hw, analysis.Install, analysis.BuildIdentity, row.Game, model, builds);
            Print("Game", snapshot, RuntimeFactGroup.Game);
            Print("Compatibility", snapshot, RuntimeFactGroup.Compatibility);
            Console.WriteLine($"RUNTIME_SETTINGS_ROWS={model.Sections.Sum(section => section.Rows.Count)} NOTES_APPLICABLE={model.Notes.Count(note => note.Applicability == WikiNoteApplicability.General)} " +
                              $"NOTES_TOTAL={model.Notes.Count} ONLINE_EVIDENCE={model.Online.Count} ONLINE_APPLICABLE={model.OnlineForInstalled.Count} EXACT_BUILD_URLS={model.ExactBuildUrls?.Count ?? 0}");
        }

        var after = Fingerprint(Path.GetDirectoryName(fullExe)!);
        Assert(before == after, "The read-only acceptance run must not change any RPCS3 config or patch file.");
        Console.WriteLine("RUNTIME_RPCS3_TREE_UNCHANGED=True");
    }

    private static void Print(string title, RuntimeContextSnapshot snapshot, RuntimeFactGroup group)
    {
        foreach (var fact in snapshot.InGroup(group))
        {
            Console.WriteLine($"RUNTIME_{title.ToUpperInvariant()}: {fact.Key}={fact.Value ?? "-"} [{fact.State}{(fact.Code is null ? string.Empty : "/" + fact.Code)}]");
        }
    }

    // Names, sizes and content hashes of the mutable RPCS3 text state.
    private static string Fingerprint(string root)
    {
        var files = new List<string>();
        foreach (var folder in new[] { "config", "patches", "GuiConfigs" })
        {
            var path = Path.Combine(root, folder);
            if (Directory.Exists(path))
            {
                files.AddRange(Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories)
                    .Where(file => file.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) ||
                                   file.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase)));
            }
        }

        var text = string.Join('\n', files.OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
            .Select(file => $"{file}|{new FileInfo(file).Length}|{Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)))}"));
        return Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text))) + "/" + files.Count;
    }
}
