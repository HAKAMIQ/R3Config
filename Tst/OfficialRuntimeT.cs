using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using R3Config;
using R3Config.Application;
using R3Config.Composition;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Tests;

// OFFICIAL_RUNTIME_COMPATIBILITY_GATE evidence for two boundaries of the
// Official patch path, both exercised through the real production services on
// a temporary RPCS3 layout (never a real installation):
//  - patch.yml is RPCS3-owned: enable/apply/disable/rollback touch
//    patch_config.yml only.
//  - Wiki-derived data lives in the Game Settings/Wiki evidence domain only and
//    cannot reach the Official catalog, matches, plans, or state.
internal static class OfficialRuntimeT
{
    private const string Serial = "BLUS99010";
    private const string Version = "01.00";
    private const string OfficialTitle = "Isolation Official Patch";
    private const string WikiOnlyPatch = "Wiki Only Patch";
    private const string Hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private const string OfficialYaml =
        "Version: 1.2\r\n" +
        "PPU-" + Hash + ":\r\n" +
        "  \"" + OfficialTitle + "\":\r\n" +
        "    Games:\r\n" +
        "      \"Isolation Game\":\r\n" +
        "        " + Serial + ": [ " + Version + " ]\r\n" +
        "    Author: \"RPCS3\"\r\n" +
        "    Patch Version: 1.0\r\n" +
        "    Patch:\r\n" +
        "      - [ be32, 0x00001000, 0x60000000 ]\r\n";

    private const string ExistingPatchConfig =
        "PPU-existing:\r\n" +
        "  Existing Patch:\r\n" +
        "    Existing Game:\r\n" +
        "      BLUS10000:\r\n" +
        "        01.00:\r\n" +
        "          Enabled: true\r\n";

    private sealed record FileState(string Sha256, long Length, DateTime LastWriteUtc);

    // ---------------------------------------------------------------------
    // 1. PATCH_YML_READ_ONLY
    // ---------------------------------------------------------------------
    public static void PatchYmlReadOnly(string tempRoot)
    {
        var (install, officialPath) = MakeInstall(Path.Combine(tempRoot, "patch-yml-readonly"));
        File.WriteAllText(install.PatCfg, ExistingPatchConfig, new UTF8Encoding(false));
        var patchConfigBefore = File.ReadAllText(install.PatCfg);

        var officialBefore = State(officialPath);
        var match = OfficialMatch(officialPath);
        Assert(match.Status == MatStat.Match && PatAct.Can(match),
            "The Official fixture patch must be an exact, activatable match before mutation.");

        var useCase = NewUseCase(new PatchApplyService());

        void AssertOfficialUntouched(string phase)
        {
            Assert(State(officialPath) == officialBefore,
                $"patch.yml bytes/hash/length/timestamp must be identical after Official {phase}.");
            Assert(Directory.GetFiles(install.PatPath).Select(Path.GetFileName)
                       .SequenceEqual(["patch.yml"]),
                $"Official {phase} must not create any file next to patch.yml.");
        }

        // Enable/apply through the production transaction path.
        var enablePlan = useCase.BuildOn(install, [match], [], expertMode: false, allMatches: [match]);
        Assert(enablePlan.HasChg &&
               enablePlan.Changes.Single().PatTitle == OfficialTitle &&
               Path.GetFullPath(enablePlan.PatCfg) == Path.GetFullPath(install.PatCfg),
            "The Official enable plan must target patch_config.yml with exactly the selected patch.");
        var enabled = useCase.ApplyTxn(enablePlan, install, [match]);
        Assert(enabled.Backup.Files.Select(file => Path.GetFullPath(file.TgtPath))
                   .SequenceEqual([Path.GetFullPath(install.PatCfg)]),
            "The Official apply transaction must back up and mutate patch_config.yml only.");
        Assert(new CfgRead().IsOn(install.PatCfg, enablePlan.Changes.Single()) &&
               File.ReadAllText(install.PatCfg).Contains("Existing Patch", StringComparison.Ordinal),
            "Official apply must enable the patch in patch_config.yml and keep unrelated entries.");
        AssertOfficialUntouched("apply");

        // Disable.
        var disablePlan = useCase.BuildOff(install, [match], [match], [match]);
        var disabled = useCase.Disable(disablePlan, install, [match]);
        Assert(disabled.Backup.Files.Select(file => Path.GetFullPath(file.TgtPath))
                   .SequenceEqual([Path.GetFullPath(install.PatCfg)]) &&
               !new CfgRead().IsOn(install.PatCfg, enablePlan.Changes.Single()) &&
               File.ReadAllText(install.PatCfg).Contains("Existing Patch", StringComparison.Ordinal),
            "Official disable must change patch_config.yml only, removing just the enabled state.");
        AssertOfficialUntouched("disable");

        // Rollback: restore the apply backup, then a failed transaction.
        new BackupService().Restore(enabled.Backup);
        Assert(File.ReadAllText(install.PatCfg) == patchConfigBefore,
            "Restoring the Official apply backup must recover the previous patch_config.yml.");
        AssertOfficialUntouched("backup restore");

        var failingPlan = useCase.BuildOn(install, [match], [], expertMode: false, allMatches: [match]);
        var failingUseCase = NewUseCase(new PatchApplyService(
            transaction: new PatchFileTransactionService(
                commitFile: (staged, target) =>
                {
                    File.Copy(staged, target, overwrite: true);
                    throw new IOException("Simulated Official commit failure.");
                })));
        try
        {
            failingUseCase.ApplyTxn(failingPlan, install, [match]);
            throw new InvalidOperationException("Expected the simulated commit failure to surface.");
        }
        catch (IOException ex) when (ex.Message.Contains("Simulated Official", StringComparison.Ordinal))
        {
        }

        Assert(File.ReadAllText(install.PatCfg) == patchConfigBefore,
            "A failed Official transaction must roll patch_config.yml back.");
        AssertOfficialUntouched("failed transaction rollback");

        Console.WriteLine("PATCH_YML_READ_ONLY PASS: patch.yml byte-identical across Official apply, disable, restore, and failed-transaction rollback; only patch_config.yml was ever the target.");
    }

    // ---------------------------------------------------------------------
    // 2. WIKI_OFFICIAL_ISOLATION
    // ---------------------------------------------------------------------
    public static void WikiOfficialIsolation(string tempRoot)
    {
        var root = Path.Combine(tempRoot, "wiki-official-isolation");
        var (install, officialPath) = MakeInstall(root);
        var flow = new AppFlow(AppComposition.Build(new Rpcs3MutationGuard(() => false)));
        var match = OfficialMatch(officialPath);

        var officialBefore = State(officialPath);
        var catalogBefore = Signature(flow.LoadPat(install));
        var matchBefore = MatchSignature(match);
        var useCase = NewUseCase(new PatchApplyService());
        var planBefore = useCase.BuildOn(install, [match], [], expertMode: false, allMatches: [match]);

        // Real Wiki-derived data for the same game, including patch pointers,
        // a patch-dependent setting row, and a patch-specific note.
        var xmlPath = Path.Combine(root, "isolation-wiki.xml");
        File.WriteAllText(
            xmlPath,
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<mediawiki>\n  <page>\n    <ns>0</ns>\n" +
            "    <title>Isolation Game</title>\n    <revision>\n      <text xml:space=\"preserve\">{{vginfobox\n" +
            "|gameid = {{gameid|id=" + Serial + "}}\n}}\n==Configuration==\n{{config\n" +
            "|framelimit = 30\n|ppu decoder = Recompiler (LLVM)\n" +
            "|recommended patch = " + WikiOnlyPatch + "\n|highly recommended patch = " + WikiOnlyPatch + "\n" +
            "|resolution scale = 200\n|resolution scale notes = Requires the " + WikiOnlyPatch + " to work.\n}}\n" +
            "==Known Issues==\nEnable the " + WikiOnlyPatch + " before playing.\n</text>\n    </revision>\n  </page>\n</mediawiki>\n",
            new UTF8Encoding(false));

        var analysis = WikiSvc.Analyze(xmlPath, "2026-09-07");
        var rec = analysis.CatalogRecommendations.Single(item => item.Serial == Serial);

        // The Wiki analysis keeps patch pointers as evidence and never as a setting.
        var pointers = analysis.Recommendations
            .Where(entry => entry.WikiKey is "recommended patch" or "highly recommended patch")
            .ToArray();
        Assert(pointers.Length == 2 &&
               pointers.All(entry =>
                   entry.ParserStatus == WikiParserStatus.Unsupported &&
                   entry.MappedSettings.Count == 0 &&
                   !entry.IncludedInCatalog),
            "Wiki patch-pointer rows must stay unsupported evidence: no mapped setting, not in the catalog.");
        Assert(rec.Changes.All(change =>
                   change.Class is WikiRecommendationClass.MandatoryGameConfig or
                       WikiRecommendationClass.GeneralRecommendation &&
                   !change.Key.Contains("patch", StringComparison.OrdinalIgnoreCase)) &&
               rec.Changes.Any(change => change.Key == "Frame limit" && change.Value == "30") &&
               rec.Changes.Any(change => change.Key == "PPU Decoder"),
            "Wiki settings for the game must be plain Game Settings rows with no patch content.");

        var page = WikiNotesParser.Parse("Isolation Game", [Serial], File.ReadAllText(xmlPath));
        Assert(page.Notes.Any(note =>
                   note.Class == WikiNoteClass.PatchSpecificNote &&
                   note.Text.Contains(WikiOnlyPatch, StringComparison.Ordinal)),
            "The patch-specific Wiki note must be extracted as a note.");

        // Load the SHIPPED Wiki catalog + evidence and project them through the
        // application path (Minecraft and this game), then re-read Official.
        var loadedCatalog = flow.LoadCat(AppContext.BaseDirectory).Cat
            ?? throw new InvalidOperationException("Shipped cat.json must load.");
        var loadedEvidence = flow.LoadWikiEvidence(AppContext.BaseDirectory).Catalog;
        var evidence = WikiEvidenceCatalog.FromPages("RPCS3 Wiki snapshot", "2026-09-07", "test", [page]);
        var game = new Rpcs3GameDescriptor(Serial, "Isolation Game", root, Version, Hash, null, HashEvi: HashEvi.Meta);
        var plan = flow.CfgPlan(install, game, rec, null);
        var model = flow.GameSettings(plan, game, rec, evidence, null);
        Assert(model.Notes.Any(note => note.Note.Text.Contains(WikiOnlyPatch, StringComparison.Ordinal)) &&
               model.Sections.SelectMany(section => section.Rows)
                   .All(row => !row.Entry.Key.Contains("patch", StringComparison.OrdinalIgnoreCase)),
            "The Wiki patch note must live in the Game Settings notes, never as a setting row.");
        var minecraft = loadedCatalog.Find("BLES01976")
            ?? throw new InvalidOperationException("Shipped catalog must contain Minecraft.");
        _ = flow.GameSettings(
            null,
            new Rpcs3GameDescriptor("BLES01976", minecraft.WikiName, root, "01.00", null, null),
            minecraft,
            loadedEvidence,
            null);

        // Mutate through the Wiki (Game Settings) path itself.
        var wikiChanges = plan.Items.Where(item => item.IsAction).Select(item => item.Change).ToArray();
        Assert(wikiChanges.Length >= 2, "The Wiki settings must be applicable through Game Settings.");
        var wikiApply = flow.ApplyCfg(plan, wikiChanges, install);
        var customConfig = Path.Combine(install.CustPath, $"config_{Serial}.yml");
        Assert(wikiApply.Backup.Files.Select(file => Path.GetFullPath(file.TgtPath))
                   .SequenceEqual([Path.GetFullPath(customConfig)]) &&
               File.ReadAllText(customConfig).Contains("Frame limit", StringComparison.Ordinal),
            "Applying Wiki settings must mutate only the game's custom config.");

        // The Official domain is unchanged in every observable way.
        Assert(State(officialPath) == officialBefore,
            "Wiki activity must not touch patch.yml.");
        Assert(!File.Exists(install.PatCfg),
            "Wiki activity must not create or change patch_config.yml.");
        var catalogAfter = Signature(flow.LoadPat(install));
        Assert(catalogAfter.SequenceEqual(catalogBefore),
            "The Official patch catalog must be identical before and after any Wiki data or settings activity.");
        var loaded = flow.LoadPat(install).Cat!;
        Assert(loaded.Entries.All(entry =>
                   !entry.Title.Contains(WikiOnlyPatch, StringComparison.OrdinalIgnoreCase) &&
                   (entry.Notes is null || !entry.Notes.Contains(WikiOnlyPatch, StringComparison.OrdinalIgnoreCase))),
            "No Wiki-derived patch may appear as an Official catalog entry.");
        var officialEntries = loaded.Entries.Where(entry => entry.Source == PatSrc.Official).ToArray();
        var officialRead = new ReadSvc().Read(officialPath, PatSrc.Official).Catalog.Entries;
        Assert(officialEntries.Length == officialRead.Count &&
               officialEntries.Select(entry => entry.Title).Order().SequenceEqual(officialRead.Select(entry => entry.Title).Order()),
            "The Official catalog must contain exactly patch.yml's entries.");

        var matchAfter = new PatchMatchService().Match(officialRead.Single(), [game]);
        Assert(MatchSignature(matchAfter) == matchBefore,
            "Official match status and compatibility decision must not change because Wiki data exists.");
        var planAfter = useCase.BuildOn(install, [matchAfter], [], expertMode: false, allMatches: [matchAfter]);
        Assert(planAfter.Changes.Select(change => change.PatTitle).SequenceEqual(planBefore.Changes.Select(change => change.PatTitle)) &&
               planAfter.Changes.Single().PatTitle == OfficialTitle &&
               Path.GetFullPath(planAfter.PatCfg) == Path.GetFullPath(install.PatCfg),
            "An Official activation request must contain only the Official patch and target patch_config.yml, regardless of Wiki data.");

        Console.WriteLine("WIKI_OFFICIAL_ISOLATION PASS: Wiki settings/notes/patch pointers stayed in the Game Settings domain; Official catalog, match, plan, and state were unchanged.");
    }

    // ---------------------------------------------------------------------

    private static (Rpcs3InstallDescriptor Install, string OfficialPath) MakeInstall(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "config"));
        Directory.CreateDirectory(Path.Combine(root, "patches"));
        var exe = Path.Combine(root, "rpcs3.exe");
        File.WriteAllBytes(exe, []);
        var install = Rpcs3InstallService.FromExe(exe);
        var officialPath = Path.Combine(install.PatPath, "patch.yml");
        File.WriteAllText(officialPath, OfficialYaml, new UTF8Encoding(false));
        return (install, officialPath);
    }

    private static PatMatch OfficialMatch(string officialPath)
    {
        var read = new ReadSvc().Read(officialPath, PatSrc.Official);
        Assert(read.IsOk && read.Catalog.Entries.Count == 1 && read.Catalog.Version == "1.2",
            "The Official fixture must parse as schema 1.2 with one entry.");
        var game = new Rpcs3GameDescriptor(
            Serial,
            "Isolation Game",
            Path.GetDirectoryName(officialPath)!,
            Version,
            Hash,
            null,
            HashEvi: HashEvi.Meta);
        return new PatchMatchService().Match(read.Catalog.Entries.Single(), [game]);
    }

    private static PatchMutationUseCase NewUseCase(PatchApplyService apply) =>
        new(
            new PatPlnr(),
            apply,
            new BundledCanaryPatchService(),
            new BundledCheatPatchService(),
            new Rpcs3MutationGuard(() => false));

    private static FileState State(string path)
    {
        var bytes = File.ReadAllBytes(path);
        return new FileState(
            Convert.ToHexString(SHA256.HashData(bytes)),
            bytes.LongLength,
            File.GetLastWriteTimeUtc(path));
    }

    private static string[] Signature(PatchCatalogResult result) =>
        [.. (result.Cat?.Entries ?? [])
            .Select(entry => $"{entry.Source}|{entry.RunKey}|{entry.Title}")
            .Order(StringComparer.Ordinal)];

    private static string MatchSignature(PatMatch match) =>
        $"{match.Status}|{match.Cmp?.Verdict}|{match.Cmp?.Code}|{match.Cmp?.CanOn}|{match.Cmp?.CanOff}|{match.TgtSer}|{match.TgtVer}";
}
