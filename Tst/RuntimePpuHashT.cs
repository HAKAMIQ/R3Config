using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;
using R3Config.Vm;

// The selected game's PPU executable hash in the Runtime tab (Game group):
// only trusted hashes (Rpcs3GameDescriptor.PpuList plus a saved H-Canary
// binding) are shown and copyable, each with its source and time; untrusted
// per-game cache hashes are never presented as the game's hash. Called from
// RuntimeContextT.Run. Synthetic fixtures only; no real clipboard.
internal static class RuntimePpuHashT
{
    private const string Serial = "TEST00004";
    private const string LogHash = "1111111111111111111111111111111111111111";
    private const string BindingHash = "2222222222222222222222222222222222222222";
    private const string CacheHash = "3333333333333333333333333333333333333333";

    private static readonly DateTime EvidenceUtc = new(2026, 11, 2, 18, 30, 0, DateTimeKind.Utc);

    public static void Run()
    {
        AssertTrustedSingle();
        AssertSeveralTrusted();
        AssertCacheOnlyIsNotPresented();
        AssertNoHash();
        AssertViewModelCopy();
        AssertClipboardBusyIsCaught();
        Console.WriteLine("Runtime PPU hash (trusted only, sources, cache-only not presented, swappable copy) PASS.");
    }

    internal static Rpcs3GameDescriptor TrustedLogGame() =>
        new(Serial, "Synthetic Game", string.Empty, "1.05", LogHash, null,
            PpuRaw: [LogHash], HashEvi: HashEvi.PlainLog, HashTime: EvidenceUtc, AuthPpu: [LogHash]);

    // As PatchHashProbeService.Enrich leaves it when only the bounded cache
    // probe found a hash that is not proven current for the installed EBOOT.
    internal static Rpcs3GameDescriptor CacheOnlyGame() =>
        new(Serial, "Synthetic Game", string.Empty, "1.05", CacheHash, null,
            PpuRaw: [CacheHash], HashEvi: HashEvi.Cache, HashTime: EvidenceUtc, AuthPpu: []);

    internal static string Hash => LogHash;

    internal static string UntrustedHash => CacheHash;

    private static RuntimeContextSnapshot Build(Rpcs3GameDescriptor? game) =>
        RuntimeContextBuilder.Build(null, null, null, game, null, null);

    private static RuntimeFact Fact(RuntimeContextSnapshot snapshot, string key) =>
        snapshot.Find(key) ?? throw new InvalidOperationException($"Missing runtime fact {key}.");

    private static void AssertTrustedSingle()
    {
        var snapshot = Build(TrustedLogGame());
        Assert(snapshot.PpuHashes.Count == 1 &&
               snapshot.PpuHashes[0] == new RuntimePpuHash(LogHash, RuntimePpuHashSource.Rpcs3Runtime, EvidenceUtc),
            "One trusted log hash is listed once with the RPCS3 log/cache source and the evidence time.");
        Assert(Fact(snapshot, "GamePpuHash").Value == LogHash && Fact(snapshot, "GamePpuHash").State == EvidenceState.Known &&
               snapshot.GameIdentityState == EvidenceState.Exact,
            "A trusted hash completes the game identity.");

        var metadata = Build(new Rpcs3GameDescriptor(Serial, "T", string.Empty, "1.05", LogHash, null, HashEvi: HashEvi.Meta));
        Assert(metadata.PpuHashes.Single() == new RuntimePpuHash(LogHash, RuntimePpuHashSource.Metadata, null),
            "A hash read from game metadata is labeled as metadata and carries no invented time.");

        // HashEvi.None records no evidence kind: the source is never claimed.
        var unrecorded = Build(new Rpcs3GameDescriptor(Serial, "T", string.Empty, "1.05", LogHash, null));
        Assert(unrecorded.PpuHashes.Single() == new RuntimePpuHash(LogHash, RuntimePpuHashSource.Unknown, null),
            "A hash without recorded evidence must not be labeled as game metadata. Got: " + string.Join(" | ", unrecorded.PpuHashes));
    }

    private static void AssertSeveralTrusted()
    {
        // A saved H-Canary binding rewrites the descriptor's evidence kind and
        // time, so the probe's own trusted hash keeps only a generic source.
        var game = new Rpcs3GameDescriptor(Serial, "Synthetic Game", string.Empty, "1.05", BindingHash, null,
            PpuRaw: [LogHash, CacheHash, BindingHash], HashEvi: HashEvi.PlainLog, HashTime: EvidenceUtc,
            AuthPpu: [LogHash], HCanPpu: [BindingHash]);
        var snapshot = Build(game);
        Assert(snapshot.PpuHashes.SequenceEqual(
               [
                   new RuntimePpuHash(LogHash, RuntimePpuHashSource.Analysis, null),
                   new RuntimePpuHash(BindingHash, RuntimePpuHashSource.HCanBinding, EvidenceUtc)
               ]),
            "Every trusted hash is listed with its own source; none is picked, and the untrusted cache hash is absent. Got: " +
            string.Join(" | ", snapshot.PpuHashes));
        var fact = Fact(snapshot, "GamePpuHash").Value ?? string.Empty;
        Assert(fact.Contains(LogHash, StringComparison.Ordinal) && fact.Contains(BindingHash, StringComparison.Ordinal) &&
               !fact.Contains(CacheHash, StringComparison.Ordinal),
            "The PPU hash fact lists the trusted hashes only.");
    }

    private static void AssertCacheOnlyIsNotPresented()
    {
        foreach (var game in new[] { CacheOnlyGame(), CacheOnlyGame() with { AuthPpu = null } })
        {
            var snapshot = Build(game);
            Assert(snapshot.PpuHashes.Count == 0 &&
                   Fact(snapshot, "GamePpuHash").Value is null &&
                   Fact(snapshot, "GamePpuHash").State == EvidenceState.Unknown &&
                   snapshot.Facts.All(item => item.Value?.Contains(CacheHash, StringComparison.Ordinal) != true),
                "An untrusted per-game cache hash is never presented as the game's PPU hash.");
            Assert(snapshot.GameIdentityState == EvidenceState.Partial,
                "Without a trusted hash the game identity stays Partial.");
        }
    }

    private static void AssertNoHash()
    {
        var snapshot = Build(new Rpcs3GameDescriptor(Serial, "T", string.Empty, "1.05", null, null));
        Assert(snapshot.PpuHashes.Count == 0 && Fact(snapshot, "GamePpuHash").State == EvidenceState.Unknown,
            "No hash: nothing is listed.");
        Assert(Build(null).PpuHashes.Count == 0 && RuntimeContextSnapshot.Empty.PpuHashes.Count == 0,
            "No game: nothing is listed.");
    }

    private static RuntimeGroupVm GameGroup(RuntimeVm vm) =>
        vm.Groups.Single(group => group.Group == RuntimeFactGroup.Game);

    private static void AssertViewModelCopy()
    {
        var original = RuntimeVm.CopyText;
        var copied = new List<string>();
        RuntimeVm.CopyText = copied.Add;
        var vm = new RuntimeVm();
        try
        {
            UiText.SetLang(UiText.EnLang);
            vm.Load(Build(TrustedLogGame()));
            var game = GameGroup(vm);
            var row = game.PpuHashes.Single();
            Assert(game.HasPpuHashes && !game.HasPpuHashGuidance &&
                   row.Hash == LogHash && row.SourceText == UiText.GetText("Runtime.PpuHash.Source.Rpcs3Runtime") &&
                   row.HasTime && row.TimeText == UiDate.Text(new DateTimeOffset(EvidenceUtc)) &&
                   row.CopyLabel == UiText.GetText("Link.Copy") && game.PpuHashLabel == UiText.GetText("Runtime.PpuHash.Label"),
                "A trusted hash row shows the hash, its localized source, its time and a Copy action.");
            Assert(game.SummaryRows.Concat(game.DetailRows).All(item => item.Id != "GamePpuHash"),
                "The copyable hash rows replace the plain PPU hash fact.");
            Assert(vm.Groups.Where(group => group.Group != RuntimeFactGroup.Game)
                       .All(group => !group.HasPpuHashes && !group.HasPpuHashGuidance),
                "Only the Game group carries PPU hash rows or guidance.");

            row.CopyCmd.Execute(null);
            Assert(copied.SequenceEqual([LogHash]),
                "Copy hands exactly the trusted hash to the swappable copy action.");

            UiText.SetLang(UiText.ArabLang);
            vm.SetLang();
            var arabic = GameGroup(vm).PpuHashes.Single();
            Assert(arabic.Hash == LogHash && arabic.SourceText == UiText.GetText("Runtime.PpuHash.Source.Rpcs3Runtime") &&
                   arabic.SourceText.Any(ch => ch is >= '؀' and <= 'ۿ') && arabic.CopyLabel == "نسخ",
                "Arabic shows a natural source label while the hash stays untouched.");

            UiText.SetLang(UiText.EnLang);
            vm.Load(Build(CacheOnlyGame()));
            game = GameGroup(vm);
            Assert(!game.HasPpuHashes && game.PpuHashes.Count == 0 && game.HasPpuHashGuidance &&
                   game.PpuHashGuidance == UiText.GetText("Setup.Warning.HashUnknown") &&
                   game.SummaryRows.Concat(game.DetailRows).All(item => !item.Value.Contains(CacheHash, StringComparison.Ordinal)),
                "Cache-only evidence offers no Copy, shows the HashUnknown guidance and does not show the cache hash.");

            vm.Load(Build(null));
            Assert(GameGroup(vm) is { HasPpuHashes: false, HasPpuHashGuidance: false },
                "With no selected game there are no hash rows and no hash guidance.");

            foreach (var key in new[] { "Label", "Source.Rpcs3Runtime", "Source.HCanBinding", "Source.Metadata", "Source.Analysis", "Source.Unknown" })
            {
                foreach (var language in new[] { UiText.EnLang, UiText.ArabLang })
                {
                    UiText.SetLang(language);
                    var text = UiText.GetText($"Runtime.PpuHash.{key}");
                    Assert(!string.IsNullOrWhiteSpace(text) && !text.StartsWith("Runtime.", StringComparison.Ordinal),
                        $"Runtime.PpuHash.{key} must be localized in {language}.");
                }
            }
        }
        finally
        {
            RuntimeVm.CopyText = original;
            UiText.SetLang(UiText.EnLang);
        }
    }

    private static void AssertClipboardBusyIsCaught()
    {
        var copied = new List<string>();
        Assert(UiDlg.TryCopyText(copied.Add, LogHash) && copied.SequenceEqual([LogHash]),
            "A free clipboard receives the text.");
        Assert(!UiDlg.TryCopyText(_ => throw new ClipboardBusyException(), LogHash),
            "A busy clipboard is reported as not copied instead of crashing.");
    }

    // What Clipboard.SetText throws when another process holds the clipboard
    // (a COMException, which is an ExternalException).
    private sealed class ClipboardBusyException() : ExternalException("Clipboard busy.");
}
