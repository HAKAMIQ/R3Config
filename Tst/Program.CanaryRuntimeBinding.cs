using System.IO;
using R3Config.Application;
using R3Config.Mdl;
using R3Config.Svc;

internal static partial class Program
{
    private static void CanaryRuntimeBinding_Regressions()
    {
        var bundledCanaryDataPath = Path.Combine(
            RepoRoot(),
            "App",
            "Data",
            "can.yml");
        var bundledCanaryRead = new ReadSvc().Read(
            bundledCanaryDataPath,
            PatSrc.Can);
        Assert(
            bundledCanaryRead.IsOk,
            "Expected bundled Canary data for local runtime-binding regressions: " +
            bundledCanaryRead.Error);

        const string canonicalHash =
            "412a9bf323b4743de21658a286f71dbe82cad807";
        const string localPpu =
            "d0c3e2a107c47d20d3316325ce6f75464a8f4f1d";
        var canonicalEntries = bundledCanaryRead.Catalog.Entries
            .Where(entry =>
                entry.Hash.Equals(
                    canonicalHash,
                    StringComparison.OrdinalIgnoreCase) &&
                entry.GameTgt.Any(target => target.Serial.Equals(
                    "BCUS98119",
                    StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        Assert(
            canonicalEntries.Length == 4,
            "Expected four canonical BCUS98119 Canary definitions for the local-binding regression.");

        var exactLocalEntries = bundledCanaryRead.Catalog.Entries
            .Where(entry =>
                entry.Hash.Equals(
                    localPpu,
                    StringComparison.OrdinalIgnoreCase) &&
                entry.Title.StartsWith(
                    "(Experimental)",
                    StringComparison.OrdinalIgnoreCase) &&
                entry.GameTgt.Any(target => target.Title.Equals(
                    "inFamous",
                    StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        Assert(
            exactLocalEntries.Length == 4,
            "Expected four exact local-PPU inFamous Canary definitions for hash-first variant selection.");

        var localGame = new Rpcs3GameDescriptor(
            "BCUS98119",
            "inFamous",
            @"C:\Games\BCUS98119",
            "01.00",
            null,
            null,
            HCanPpu: [localPpu],
            HCanSpu: []);
        var boundMatches = new PatchMatchService()
            .Match(bundledCanaryRead.Catalog, localGame)
            .Where(match =>
                match.Patch.Hash.Equals(
                    localPpu,
                    StringComparison.OrdinalIgnoreCase) &&
                match.TgtSer == "BCUS98119")
            .ToArray();
        var localRunKey = RunKey.Build(
            HashKind.Ppu,
            localPpu,
            serialScope: null);
        Assert(
            boundMatches.Length == 4 &&
            boundMatches.All(match =>
                match.Status == MatStat.Match &&
                PatAct.Can(match) &&
                match.EffectiveRunKey.Equals(
                    localRunKey,
                    StringComparison.OrdinalIgnoreCase) &&
                match.Patch.RunKey.Equals(
                    localRunKey,
                    StringComparison.OrdinalIgnoreCase) &&
                exactLocalEntries.Any(exact =>
                    exact.Title.Equals(
                        match.Patch.Title,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(
                        exact.DefPrint,
                        match.Patch.DefPrint,
                        StringComparison.OrdinalIgnoreCase))),
            "Expected the saved local PPU to select the exact canonical inFamous Canary body for that executable hash, then retarget its metadata to BCUS98119 without transplanting offsets from the serial-only variant.");

        var compiled = new PatchSelectionCompiler().Compile(
            File.ReadAllText(bundledCanaryDataPath),
            boundMatches,
            boundMatches);
        Assert(
            compiled.IsOk &&
            compiled.PatText.Contains(
                $"PPU-{localPpu}",
                StringComparison.OrdinalIgnoreCase) &&
            !compiled.PatText.Contains(
                $"PPU-{canonicalHash}:",
                StringComparison.OrdinalIgnoreCase),
            "Expected prepared Canary YAML to use the exact saved local PPU variant without emitting the serial-only canonical hash: " +
            compiled.Error);

        var reboundRead = new ReadSvc().ReadText(
            compiled.PatText,
            PatSrc.Imported,
            sourcePath: null);
        Assert(
            reboundRead.IsOk &&
            reboundRead.Catalog.Entries.Count == 4,
            "Expected the exact local-hash Canary document to round-trip through the RPCS3 patch parser: " +
            reboundRead.Error);
        var reboundClassified = PatchCatalogUseCase.Classify(
            reboundRead.Catalog,
            bundledCanaryRead.Catalog);
        Assert(
            reboundClassified.Entries.All(entry =>
                entry.Class == PatClass.Canary &&
                entry.Risk == PatRisk.Canary &&
                entry.RunKey.Equals(
                    localRunKey,
                    StringComparison.OrdinalIgnoreCase)),
            "Expected a reloaded managed Canary copy to retain Canary provenance under its local PPU runtime key.");

        var reboundMerged = new PatchCatalogMergeService().Merge(
            [bundledCanaryRead.Catalog, reboundClassified]);
        var reloadMatches = new PatchMatchService()
            .Match(reboundMerged, localGame)
            .Where(match =>
                match.Status is not MatStat.SerBad and not MatStat.NoMeta &&
                match.TgtSer == "BCUS98119" &&
                boundMatches.Any(original => original.Patch.Title.Equals(
                    match.Patch.Title,
                    StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        Assert(
            reloadMatches.Length == 4 &&
            reloadMatches.All(match =>
                match.Patch.Source == PatSrc.Imported &&
                match.Status == MatStat.Match &&
                match.EffectiveRunKey.Equals(
                    localRunKey,
                    StringComparison.OrdinalIgnoreCase)),
            "Expected the resident exact-hash Canary definitions to win over bundled preview duplicates after catalog reload.");

        var change = new PatPlnr().Build3(boundMatches[0]);
        Assert(
            change is not null &&
            change.HashKey.Equals(
                localRunKey,
                StringComparison.OrdinalIgnoreCase),
            "Expected patch_config planning to use the saved local PPU runtime key after Canary rebinding.");

        var ambiguousBase = canonicalEntries[0];
        var firstHash = new string('a', 40);
        var secondHash = new string('b', 40);
        var unrelatedLocalHash = new string('c', 40);
        var first = ambiguousBase with
        {
            Hash = firstHash,
            DefPrint = "canary-definition-a"
        };
        var second = ambiguousBase with
        {
            Hash = secondHash,
            DefPrint = "canary-definition-b"
        };
        var ambiguousCatalog = new PatCat(
            "1.2",
            [first, second]);
        var ambiguousGame = localGame with
        {
            HCanPpu = [unrelatedLocalHash]
        };
        var ambiguousMatches = new PatchMatchService()
            .Match(ambiguousCatalog, ambiguousGame);
        Assert(
            ambiguousMatches.Count == 2 &&
            ambiguousMatches.All(match =>
                match.Status == MatStat.HashBad &&
                match.Cmp is
                {
                    Verdict: PatchCompatibilityVerdict.Blocked,
                    Code: PatchCompatibilityReasonCode.CanaryBindingAmbiguous,
                    CanOn: false,
                    UseGuard: false
                }),
            "Expected different Canary bodies targeting the same serial/version/title to fail closed when the saved local PPU does not identify one exactly.");

        var multipleLocalPpuGame = localGame with
        {
            HCanPpu =
            [
                localPpu,
                new string('e', 40)
            ]
        };
        var multipleLocalMatches = new PatchMatchService()
            .Match(
                new PatCat(
                    bundledCanaryRead.Catalog.Version,
                    canonicalEntries),
                multipleLocalPpuGame);
        Assert(
            multipleLocalMatches.Count == 4 &&
            multipleLocalMatches.All(match =>
                match.Cmp is
                {
                    Verdict: PatchCompatibilityVerdict.Blocked,
                    Code: PatchCompatibilityReasonCode.CanaryBindingAmbiguous,
                    CanOn: false,
                    UseGuard: false
                }),
            "Expected multiple unmatched local PPU executable hashes to fail closed instead of arbitrarily rebinding Canary to the first captured hash.");

        var exactVariantGame = ambiguousGame with
        {
            HCanPpu = [secondHash]
        };
        var exactVariantMatches = new PatchMatchService()
            .Match(ambiguousCatalog, exactVariantGame);
        Assert(
            exactVariantMatches.Count == 1 &&
            exactVariantMatches[0].Patch.Hash.Equals(
                secondHash,
                StringComparison.OrdinalIgnoreCase) &&
            exactVariantMatches[0].Status == MatStat.Match &&
            exactVariantMatches[0].EffectiveRunKey.Equals(
                $"PPU-{secondHash}",
                StringComparison.OrdinalIgnoreCase),
            "Expected an exact saved local PPU to select the matching canonical Canary variant and suppress conflicting siblings.");
    }
}
