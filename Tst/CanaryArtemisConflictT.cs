using System.IO;
using System.Linq;
using R3Config.Application;
using R3Config.Mdl;
using R3Config.Svc;
using R3Config.Vm;

namespace R3Config.Tests;

internal static class CanaryArtemisConflictT
{
    private const string Serial = "BLUS12345";
    private const string Version = "01.00";
    private const string HashA = "1111111111111111111111111111111111111111";
    private const string HashB = "2222222222222222222222222222222222222222";

    public static void Run(string tempRoot)
    {
        ExactEquivalent_BlocksOnlyArtemisEnable();
        TitleOnly_DifferentWritesRemainActionable();
        DifferentSerial_DoesNotBlockCurrentRuntime();
        DifferentAppVersion_DoesNotBlockCurrentRuntime();
        DifferentPpuHash_DoesNotBlockCurrentRuntime();
        LoadWrapper_NormalizesToEquivalent();
        PartialOverlap_IsNotEquivalent();
        LegacyEnabledArtemis_CanDisableThenCannotReenable(tempRoot);
        ResidentManagedArtemisCopy_CanDisableThenCannotReenable(tempRoot);
        NoCanaryEquivalent_ArtemisUnchanged();
        UnknownComparison_DoesNotBlock();
        PrerequisiteMismatch_DoesNotClaimEquivalence();
        ShippedGodOfWarUnlockFps_IsCanaryOwned();
        ShippedRatchetFreezeFix_IsCanaryOwned();
        MutationBoundaries_RejectEquivalentArtemisEnable(tempRoot);

        Console.WriteLine(
            "Canary/Artemis semantic ownership and mutation defense PASS. Passed: 15, Failed: 0, Skipped: 0");
    }

    private static void ExactEquivalent_BlocksOnlyArtemisEnable()
    {
        var canary = Entry(
            PatSrc.Can,
            "Canonical Feature",
            Serial,
            Version,
            HashA,
            "      - [ be32, 0x00001000, 0x60000000 ]");
        var artemis = Entry(
            PatSrc.Artemis,
            "Different Artemis Name",
            Serial,
            Version,
            HashA,
            "      - [ byte, 0x00001000, 0x60 ]\n" +
            "      - [ byte, 0x00001001, 0x00 ]\n" +
            "      - [ byte, 0x00001002, 0x00 ]\n" +
            "      - [ byte, 0x00001003, 0x00 ]");

        var result = Match(canary, artemis, Game(Serial, Version, HashA));

        Assert(PatAct.Can(result.Canary) &&
               result.Canary.Cmp is
               {
                   Code: PatchCompatibilityReasonCode.ExactId,
                   CanOn: true,
                   CanOff: true
               },
            "The applicable Canary definition must remain normally actionable.");
        Assert(result.Artemis.Patch.Source == PatSrc.Artemis &&
               result.Artemis.Cmp is
               {
                   Verdict: PatchCompatibilityVerdict.Blocked,
                   Code: PatchCompatibilityReasonCode.CanaryEquivalent,
                   CanOn: false,
                   CanOff: true
               },
            "A proven equivalent must retain Artemis provenance while only its enable action is blocked.");
    }

    private static void TitleOnly_DifferentWritesRemainActionable()
    {
        var canary = Entry(
            PatSrc.Can,
            "Same Name",
            Serial,
            Version,
            HashA,
            "      - [ be32, 0x00001000, 0x60000000 ]");
        var artemis = Entry(
            PatSrc.Artemis,
            "Same Name",
            Serial,
            Version,
            HashA,
            "      - [ be32, 0x00002000, 0x60000000 ]");

        var result = Match(canary, artemis, Game(Serial, Version, HashA));

        Assert(PatAct.Can(result.Artemis) &&
               result.Artemis.Cmp?.Code != PatchCompatibilityReasonCode.CanaryEquivalent,
            "Title equality without write equality must not block Artemis.");
    }

    private static void DifferentSerial_DoesNotBlockCurrentRuntime()
    {
        var canary = Entry(
            PatSrc.Can,
            "Same Feature",
            "BLES54321",
            Version,
            HashA,
            "      - [ be32, 0x00001000, 0x60000000 ]");
        var artemis = Entry(
            PatSrc.Artemis,
            "Same Feature",
            Serial,
            Version,
            HashA,
            "      - [ be32, 0x00001000, 0x60000000 ]");

        var result = Match(canary, artemis, Game(Serial, Version, HashA));

        Assert(result.Canary.Status == MatStat.SerBad &&
               PatAct.Can(result.Artemis) &&
               result.Artemis.Cmp?.Code != PatchCompatibilityReasonCode.CanaryEquivalent,
            "A Canary definition for another exact serial must not block Artemis for the selected game.");
    }

    private static void DifferentAppVersion_DoesNotBlockCurrentRuntime()
    {
        var canary = Entry(
            PatSrc.Can,
            "Same Feature",
            Serial,
            "01.01",
            HashA,
            "      - [ be32, 0x00001000, 0x60000000 ]");
        var artemis = Entry(
            PatSrc.Artemis,
            "Same Feature",
            Serial,
            Version,
            HashA,
            "      - [ be32, 0x00001000, 0x60000000 ]");

        var result = Match(canary, artemis, Game(Serial, Version, HashA));

        Assert(result.Canary.Status == MatStat.VerBad &&
               PatAct.Can(result.Artemis) &&
               result.Artemis.Cmp?.Code != PatchCompatibilityReasonCode.CanaryEquivalent,
            "A Canary definition for another APP_VER must not block Artemis for the selected version.");
    }

    private static void DifferentPpuHash_DoesNotBlockCurrentRuntime()
    {
        var canary = Entry(
            PatSrc.Can,
            "Same Feature",
            Serial,
            Version,
            HashA,
            "      - [ be32, 0x00001000, 0x60000000 ]");
        var artemis = Entry(
            PatSrc.Artemis,
            "Same Feature",
            Serial,
            Version,
            HashB,
            "      - [ be32, 0x00001000, 0x60000000 ]");

        var result = Match(canary, artemis, Game(Serial, Version, HashB));

        Assert(!result.Canary.Patch.RunKey.Equals(
                   result.Artemis.Patch.RunKey,
                   StringComparison.OrdinalIgnoreCase) &&
               result.Canary.EffectiveRunKey.Equals(
                   result.Artemis.EffectiveRunKey,
                   StringComparison.OrdinalIgnoreCase) &&
               PatAct.Can(result.Canary) &&
               PatAct.Can(result.Artemis) &&
               result.Artemis.Cmp?.Code != PatchCompatibilityReasonCode.CanaryEquivalent,
            "A Canary definition rebound from another declared PPU hash must not establish exact executable-scope equivalence with Artemis.");
    }

    private static void LoadWrapper_NormalizesToEquivalent()
    {
        var canary = Entry(
            PatSrc.Can,
            "Direct",
            Serial,
            Version,
            HashA,
            "      - [ be32, 0x00001000, 0x60000000 ]");
        var artemis = Entry(
            PatSrc.Artemis,
            "Wrapped",
            Serial,
            Version,
            HashA,
            "      - [ load, [ [ byte, 0x00001000, 0x60 ], [ byte, 0x00001001, 0x00 ], [ byte, 0x00001002, 0x00 ], [ byte, 0x00001003, 0x00 ] ] ]");

        var result = Match(canary, artemis, Game(Serial, Version, HashA));

        Assert(canary.WritePrint == artemis.WritePrint &&
               PatchWriteSemantics.IsProven(canary.WritePrint) &&
               result.Artemis.Cmp?.Code == PatchCompatibilityReasonCode.CanaryEquivalent,
            "A modifier-free load wrapper with the same effective bytes must be proven equivalent.");
    }

    private static void PartialOverlap_IsNotEquivalent()
    {
        var canary = Entry(
            PatSrc.Can,
            "A plus B",
            Serial,
            Version,
            HashA,
            "      - [ byte, 0x00001000, 0x11 ]\n" +
            "      - [ byte, 0x00001001, 0x22 ]");
        var artemis = Entry(
            PatSrc.Artemis,
            "A plus C",
            Serial,
            Version,
            HashA,
            "      - [ byte, 0x00001000, 0x11 ]\n" +
            "      - [ byte, 0x00001002, 0x33 ]");

        var result = Match(canary, artemis, Game(Serial, Version, HashA));

        Assert(PatchWriteSemantics.Classify(
                   canary.WritePrint,
                   artemis.WritePrint) == PatchWriteRelation.Unknown &&
               PatAct.Can(result.Artemis) &&
               result.Artemis.Cmp?.Code != PatchCompatibilityReasonCode.CanaryEquivalent,
            "Partial write overlap must remain unproven and must not fire the Canary-equivalent rule.");
    }

    private static void LegacyEnabledArtemis_CanDisableThenCannotReenable(
        string tempRoot)
    {
        var result = Equivalent();
        var disabled = new PatItVm(result.Artemis, isEnabled: false);
        var enabled = new PatItVm(result.Artemis, isEnabled: true);

        Assert(!disabled.CanOn && !disabled.CanOff && !disabled.CanSel,
            "A disabled equivalent Artemis row must not be enableable.");
        Assert(!enabled.CanOn && enabled.CanOff && enabled.CanSel,
            "An already enabled equivalent Artemis row must remain selectable for disable.");

        var install = Install(Path.Combine(tempRoot, "legacy-disable"));
        var mutation = Mutation();
        var disablePlan = mutation.BuildOff(
            install,
            [result.Artemis],
            enabledMatches: [result.Artemis],
            allMatches: result.All);

        Assert(disablePlan.HasChg && disablePlan.Changes.Count == 1,
            "The normal disable planner must retain one removal change for a legacy enabled Artemis equivalent.");
        Assert(!new PatItVm(result.Artemis, isEnabled: false).CanOn,
            "After disable, the same Artemis equivalent must remain blocked from re-enable.");
    }

    // A legacy enabled Artemis cheat always has a resident R3Config-managed
    // copy in <SERIAL>_patch.yml, and the catalog merge keeps that resident
    // copy (ManagedArtemis) over the bundled art.yml row. The re-enable block
    // must therefore hold for the resident copy the main list actually shows.
    private static void ResidentManagedArtemisCopy_CanDisableThenCannotReenable(
        string tempRoot)
    {
        const string operations = "      - [ be32, 0x00001000, 0x60000000 ]";
        var canary = Entry(PatSrc.Can, "Canary owner", Serial, Version, HashA, operations);
        var bundled = Entry(PatSrc.Artemis, "Artemis copy", Serial, Version, HashA, operations);
        var resident = Entry(PatSrc.ManagedArtemis, "Artemis copy", Serial, Version, HashA, operations);
        var merged = new PatchCatalogMergeService().Merge(
            new PatCat("1.2", [canary]),
            new PatCat("1.2", [bundled]),
            new PatCat("1.2", [resident]));
        var all = new PatchMatchService()
            .Match(merged, Game(Serial, Version, HashA))
            .ToArray();
        var managed = all.Single(match =>
            match.Patch.Source is PatSrc.Artemis or PatSrc.ManagedArtemis);

        Assert(managed.Patch.Source == PatSrc.ManagedArtemis,
            "The catalog merge must surface the resident managed copy over the bundled Artemis row.");
        Assert(managed.Cmp?.Code == PatchCompatibilityReasonCode.CanaryEquivalent &&
               !new PatItVm(managed, isEnabled: false).CanOn &&
               new PatItVm(managed, isEnabled: false).StateTon == "Auto",
            "A resident managed Artemis copy with an applicable Canary equivalent must not be re-enableable.");

        var enabled = new PatItVm(managed, isEnabled: true);
        Assert(!enabled.CanOn && enabled.CanOff && enabled.CanSel,
            "An already enabled resident managed Artemis copy must remain selectable for disable.");

        var install = Install(Path.Combine(tempRoot, "resident-managed"));
        Directory.CreateDirectory(install.CfgPath);
        Directory.CreateDirectory(install.PatPath);
        var mutation = Mutation();
        ThrowsCanaryEquivalent(() => mutation.BuildOn(
            install,
            [managed with { Cmp = null }],
            enabledMatches: [],
            expertMode: false,
            allMatches: all));

        var disablePlan = mutation.BuildOff(
            install,
            [managed],
            enabledMatches: [managed],
            allMatches: all);
        Assert(disablePlan.HasChg && disablePlan.Changes.Count == 1,
            "The normal disable planner must retain one removal change for an enabled resident managed Artemis copy.");
        Assert(!File.Exists(install.PatCfg) &&
               Directory.GetFiles(install.PatPath).Length == 0,
            "Rejected enable planning must not create or modify user patch state.");
    }

    private static void NoCanaryEquivalent_ArtemisUnchanged()
    {
        var artemis = Entry(
            PatSrc.Artemis,
            "Artemis Only",
            Serial,
            Version,
            HashA,
            "      - [ be32, 0x00001000, 0x60000000 ]");
        var match = new PatchMatchService()
            .Match(new PatCat("1.2", [artemis]), Game(Serial, Version, HashA))
            .Single();

        Assert(PatAct.Can(match) &&
               match.Cmp is
               {
                   Code: PatchCompatibilityReasonCode.ExactId,
                   CanOn: true,
                   CanOff: true
               },
            "An Artemis definition without a Canary equivalent must retain its prior actionability.");
    }

    private static void UnknownComparison_DoesNotBlock()
    {
        var canary = Entry(
            PatSrc.Can,
            "Unknown Canary",
            Serial,
            Version,
            HashA,
            "      - [ utf8, 0x00001000, text ]");
        var artemis = Entry(
            PatSrc.Artemis,
            "Unknown Artemis",
            Serial,
            Version,
            HashA,
            "      - [ utf8, 0x00001000, text ]");

        var result = Match(canary, artemis, Game(Serial, Version, HashA));

        Assert(PatchWriteSemantics.Classify(
                   canary.WritePrint,
                   artemis.WritePrint) == PatchWriteRelation.Unknown &&
               PatAct.Can(result.Artemis) &&
               result.Artemis.Cmp?.Code != PatchCompatibilityReasonCode.CanaryEquivalent,
            "An unsupported or ambiguous write comparison must remain Unknown and preserve Artemis behavior.");
    }

    private static void PrerequisiteMismatch_DoesNotClaimEquivalence()
    {
        var canary = Entry(
            PatSrc.Can,
            "No prerequisite",
            Serial,
            Version,
            HashA,
            "      - [ be32, 0x00001000, 0x60000000 ]");
        var artemis = Entry(
            PatSrc.Artemis,
            "Requires setup",
            Serial,
            Version,
            HashA,
            "      - [ be32, 0x00001000, 0x60000000 ]") with
        {
            DepList = ["Required setup"]
        };

        var result = Match(canary, artemis, Game(Serial, Version, HashA));

        Assert(PatAct.Can(result.Artemis) &&
               result.Artemis.Cmp?.Code != PatchCompatibilityReasonCode.CanaryEquivalent,
            "Write equality with unresolved prerequisite semantics must not be promoted to proven equivalence.");
    }

    private static void ShippedGodOfWarUnlockFps_IsCanaryOwned()
    {
        var paths = ShippedPaths();
        var beforeCanary = File.ReadAllBytes(paths.Canary);
        var beforeArtemis = File.ReadAllBytes(paths.Artemis);
        var canaryCatalog = Read(paths.Canary, PatSrc.Can);
        var artemisCatalog = Read(paths.Artemis, PatSrc.Artemis);
        const string hash = "3a0b43e4a5f4bfea64f53612ee7c5d990f88129c";
        var canary = canaryCatalog.Entries.Single(entry =>
            entry.Hash.Equals(hash, StringComparison.OrdinalIgnoreCase) &&
            entry.Title == "Unlock FPS");
        var artemis = artemisCatalog.Entries.Single(entry =>
            entry.Hash.Equals(hash, StringComparison.OrdinalIgnoreCase) &&
            entry.Title == "Unlock FPS");
        var result = Match(
            canary,
            artemis,
            Game("BCUS98232", "01.12", hash));

        Assert(PatchWriteSemantics.IsProven(canary.WritePrint) &&
               canary.WritePrint == artemis.WritePrint,
            "The shipped direct and load-wrapped God of War Unlock FPS operations must normalize identically.");
        Assert(PatAct.Can(result.Canary) &&
               result.Artemis.Patch.Source == PatSrc.Artemis &&
               result.Artemis.Cmp?.Code == PatchCompatibilityReasonCode.CanaryEquivalent &&
               !new PatItVm(result.Artemis).CanOn,
            "Shipped God of War Unlock FPS must remain visible as Artemis but be actionable only through Canary.");
        Assert(beforeCanary.SequenceEqual(File.ReadAllBytes(paths.Canary)) &&
               beforeArtemis.SequenceEqual(File.ReadAllBytes(paths.Artemis)),
            "Shipped catalog discovery and projection must be byte-for-byte read-only.");
    }

    private static void ShippedRatchetFreezeFix_IsCanaryOwned()
    {
        var paths = ShippedPaths();
        var canaryCatalog = Read(paths.Canary, PatSrc.Can);
        var artemisCatalog = Read(paths.Artemis, PatSrc.Artemis);
        const string hash = "fadb0af6fb7bd0113e88fb9af3eb78fe3be05d08";
        var canary = canaryCatalog.Entries.Single(entry =>
            entry.Hash.Equals(hash, StringComparison.OrdinalIgnoreCase) &&
            entry.Title.Equals("Freeze fix", StringComparison.OrdinalIgnoreCase));
        var artemis = artemisCatalog.Entries.Single(entry =>
            entry.Hash.Equals(hash, StringComparison.OrdinalIgnoreCase) &&
            entry.Title.Equals("Freeze fix", StringComparison.OrdinalIgnoreCase));
        var result = Match(
            canary,
            artemis,
            Game("NPUA80908", "01.01", hash));

        Assert(PatchWriteSemantics.IsProven(canary.WritePrint) &&
               canary.WritePrint == artemis.WritePrint &&
               PatAct.Can(result.Canary) &&
               result.Artemis.Patch.Source == PatSrc.Artemis &&
               result.Artemis.Cmp?.Code == PatchCompatibilityReasonCode.CanaryEquivalent,
            "Shipped Ratchet & Clank Into the Nexus Freeze fix must be proven equivalent and Canary-owned.");
    }

    private static void MutationBoundaries_RejectEquivalentArtemisEnable(
        string tempRoot)
    {
        var result = Equivalent();
        var install = Install(Path.Combine(tempRoot, "mutation-defense"));
        Directory.CreateDirectory(install.CfgPath);
        Directory.CreateDirectory(install.PatPath);
        var before = Directory.GetFiles(
            Path.GetDirectoryName(install.PatCfg)!,
            "*",
            SearchOption.AllDirectories);
        var mutation = Mutation();
        var undecoratedArtemis = result.Artemis with { Cmp = null };

        ThrowsCanaryEquivalent(() => mutation.BuildOn(
            install,
            [undecoratedArtemis],
            enabledMatches: [],
            expertMode: false,
            allMatches: result.All));

        ThrowsCanaryEquivalent(() => mutation.BuildOn(
            install,
            [undecoratedArtemis],
            enabledMatches: [],
            expertMode: false));

        var change = mutation.BuildChg(undecoratedArtemis)
            ?? throw new InvalidOperationException(
                "Expected an exact Artemis change identity for mutation-boundary verification.");
        ThrowsCanaryEquivalent(() => mutation.ApplyTxn(
            new PatchApplyPlan(install.PatCfg, [change]),
            install,
            [undecoratedArtemis],
            result.All));

        var after = Directory.GetFiles(
            Path.GetDirectoryName(install.PatCfg)!,
            "*",
            SearchOption.AllDirectories);
        Assert(before.SequenceEqual(after) && !File.Exists(install.PatCfg),
            "Rejected planning and mutation revalidation must not create or modify user patch state.");
    }

    private static MatchResult Equivalent()
    {
        return Match(
            Entry(
                PatSrc.Can,
                "Canary owner",
                Serial,
                Version,
                HashA,
                "      - [ be32, 0x00001000, 0x60000000 ]"),
            Entry(
                PatSrc.Artemis,
                "Artemis copy",
                Serial,
                Version,
                HashA,
                "      - [ be32, 0x00001000, 0x60000000 ]"),
            Game(Serial, Version, HashA));
    }

    private static MatchResult Match(
        Rpcs3PatchEntry canary,
        Rpcs3PatchEntry artemis,
        Rpcs3GameDescriptor game)
    {
        var matches = new PatchMatchService()
            .Match(new PatCat("1.2", [canary, artemis]), game)
            .ToArray();
        return new MatchResult(
            matches.Single(match => match.Patch.Class == PatClass.Canary),
            matches.Single(match => match.Patch.Source is
                PatSrc.Artemis or PatSrc.ManagedArtemis),
            matches);
    }

    private static Rpcs3PatchEntry Entry(
        PatSrc source,
        string title,
        string serial,
        string version,
        string hash,
        string operations)
    {
        var yaml =
            "Version: 1.2\n" +
            $"PPU-{hash}:\n" +
            $"  \"{title}\":\n" +
            "    Games:\n" +
            "      \"Conflict Test\":\n" +
            $"        {serial}: [ {version} ]\n" +
            "    Patch Version: 1.0\n" +
            "    Patch:\n" +
            operations + "\n";
        var read = new ReadSvc().ReadText(yaml, source, source + ".yml");
        Assert(read.IsOk && read.Catalog.Entries.Count == 1,
            "Expected the conflict-policy fixture to parse: " + read.Error);
        return read.Catalog.Entries.Single();
    }

    private static Rpcs3GameDescriptor Game(
        string serial,
        string version,
        string hash)
    {
        return new Rpcs3GameDescriptor(
            serial,
            "Conflict Test",
            @"C:\Games\" + serial,
            version,
            hash,
            null,
            HCanPpu: [hash],
            HCanSpu: []);
    }

    private static Rpcs3InstallDescriptor Install(string root)
    {
        var config = Path.Combine(root, "config");
        return new Rpcs3InstallDescriptor(
            root,
            Path.Combine(root, "rpcs3.exe"),
            config,
            Path.Combine(config, "custom_configs"),
            Path.Combine(root, "patches"),
            Path.Combine(config, "patch_config.yml"),
            root);
    }

    private static PatchMutationUseCase Mutation()
    {
        return new PatchMutationUseCase(
            new PatPlnr(),
            new PatchApplyService(),
            new BundledCanaryPatchService(),
            new BundledCheatPatchService(),
            new Rpcs3MutationGuard(() => false));
    }

    private static PatCat Read(string path, PatSrc source)
    {
        var read = new ReadSvc().Read(path, source);
        Assert(read.IsOk, "Expected shipped catalog to parse: " + read.Error);
        return read.Catalog;
    }

    private static (string Canary, string Artemis) ShippedPaths()
    {
        var data = Path.Combine(AppContext.BaseDirectory, "Data");
        var canary = Path.Combine(data, "can.yml");
        var artemis = Path.Combine(data, "art.yml");
        Assert(File.Exists(canary) && File.Exists(artemis),
            "Expected shipped Canary and Artemis catalogs in the test output.");
        return (canary, artemis);
    }

    private static void ThrowsCanaryEquivalent(Action action)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("Canary equivalent", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        throw new InvalidOperationException(
            "Expected an applicable Canary equivalent to fail closed at the enable boundary.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed record MatchResult(
        PatMatch Canary,
        PatMatch Artemis,
        IReadOnlyList<PatMatch> All);
}
