using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using R3Config.Application;
using R3Config.Composition;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;
using R3Config.View;
using R3Config.Vm;
using static R3Config.Tests.TestAssert;

namespace R3Config.Tests;

// Source-occurrence acceptance for the row-manifest corpus. The raw inputs
// and archive are independently read by the shipped parser, and a real WPF
// selector verifies the final stage. No RPCS3 process is launched or modified.
internal static class NclSourceDiscoveryT
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private sealed record Source(int RowOrder, string Title, string[] Serials, string Version,
        string MatchMode, string OriginalFilename, string SourceSha256, string EntryPath,
        string SourceUrl = "");
    private sealed record Manifest(int SchemaVersion, Source[] Sources);
    private sealed record Download(int RowOrder, string Title, string ArtemisFileName,
        string Status, string SHA256, string LocalFile);
    private sealed record Target(string Serial, string Version, string Title);
    private sealed record Trace(string Serial, string Version, string Title, string Language,
        int Source, int Parser, int Corpus, int Lookup, int Vm, int Ui,
        int NonConvertible, int UiNonConvertible, int UiWhileRunning, bool ReadOnlyRootAbsent);
    private sealed record TargetTrace(string Serial, string Version, string Title,
        int SourceFiles, int Parser, int Corpus, int Lookup, int RepresentedOccurrences);

    public static void Run(string sourceDownloadRoot, string candidateZip, string outputDirectory)
    {
        var downloads = JsonSerializer.Deserialize<Download[]>(File.ReadAllText(
            Path.Combine(sourceDownloadRoot, "NclDownloadManifest.json")))
            ?? throw new InvalidDataException("Download manifest is empty.");
        var manifest = ReadManifest(candidateZip);
        var rawRoot = Path.Combine(Path.GetFullPath(sourceDownloadRoot), "Items");
        var raw = NclCorpusReader.Audit(rawRoot);
        var corpus = NclCorpusReader.Audit(candidateZip);
        var pin = corpus.SourceSha256 ?? throw new InvalidDataException("Candidate SHA missing.");
        var rawByPath = raw.Files.ToDictionary(file => Path.GetFullPath(Path.Combine(
            rawRoot, file.RelativePath)), StringComparer.OrdinalIgnoreCase);
        var corpusByPath = corpus.Files.ToDictionary(file => file.RelativePath, StringComparer.Ordinal);
        var downloadByRow = downloads.ToDictionary(row => row.RowOrder);
        Assert(manifest.SchemaVersion == 1 && manifest.Sources.Length == raw.FileCount &&
            corpus.FileCount == raw.FileCount, "Every acquired source needs exactly one manifest row and ZIP entry.");
        using (var archive = ZipFile.OpenRead(candidateZip))
        {
            foreach (var source in manifest.Sources)
            {
                var download = downloadByRow[source.RowOrder];
                var path = Path.GetFullPath(download.LocalFile);
                Assert(path.StartsWith(rawRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase),
                    "Source manifest points outside its acquired Items directory.");
                var file = rawByPath[path];
                var bundled = corpusByPath[source.EntryPath];
                var rawBytes = File.ReadAllBytes(path);
                using var entryStream = archive.GetEntry(source.EntryPath)!.Open();
                using var entryBytes = new MemoryStream();
                entryStream.CopyTo(entryBytes);
                Assert(rawBytes.AsSpan().SequenceEqual(entryBytes.ToArray()) &&
                    Sha(rawBytes) == source.SourceSha256 &&
                    Sha(rawBytes).Equals(download.SHA256, StringComparison.OrdinalIgnoreCase),
                    $"Row {source.RowOrder}: raw source bytes or provenance changed.");
                Assert(file.CheatCount == bundled.CheatCount && file.Cheats.Select(CheatKey)
                    .SequenceEqual(bundled.Cheats.Select(CheatKey), StringComparer.Ordinal),
                    $"Row {source.RowOrder}: parser definitions changed between source and corpus.");
            }
        }
        Assert(raw.CheatCount == corpus.CheatCount, "Source -> parser -> corpus lost definitions.");
        var targets = manifest.Sources.SelectMany(source => NclParser.ParseIdentity(source.OriginalFilename)
                .Targets.Select(target => new Target(target.Serial, target.Version, string.Empty)))
            .Distinct().OrderBy(target => target.Serial, StringComparer.Ordinal)
            .ThenBy(target => target.Version, StringComparer.Ordinal).ToArray();
        var targetTraces = new List<TargetTrace>();
        foreach (var target in targets)
        {
            targetTraces.Add(CheckLookup(candidateZip, pin, manifest, corpusByPath, target));
        }
        var titleTargets = manifest.Sources.Where(source => source.MatchMode == "TitleOnly")
            .Select(source => new Target("BLUS99999", PatVer.Norm(source.Version) ?? "01.00", source.Title))
            .Distinct().ToArray();
        foreach (var target in titleTargets)
        {
            targetTraces.Add(CheckLookup(candidateZip, pin, manifest, corpusByPath, target));
        }
        var representatives = manifest.Sources.Where(source =>
                corpusByPath[source.EntryPath].CheatCount > 1 && source.MatchMode == "ExactSerialVersion")
            .OrderByDescending(source => corpusByPath[source.EntryPath].CheatCount)
            .Take(3)
            .Concat(manifest.Sources.Where(source => source.Serials.Length > 1).Take(1))
            .Concat(manifest.Sources.Where(source => source.MatchMode == "TitleOnly" &&
                corpusByPath[source.EntryPath].CheatCount > 1).Take(1))
            .DistinctBy(source => source.RowOrder)
            .Select(source => ToTarget(source)).Distinct().ToArray();
        Assert(representatives.Length >= 3, "Live acceptance needs several multiple-definition games.");
        var uiTraces = CheckActualWindows(candidateZip, pin, manifest, corpusByPath, representatives);
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(Path.Combine(outputDirectory, "source-discovery.targets.json"),
            JsonSerializer.Serialize(targetTraces, JsonOptions), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(outputDirectory, "source-discovery.pipeline.json"),
            JsonSerializer.Serialize(uiTraces, JsonOptions), new UTF8Encoding(false));
        var summary = new
        {
            TOTAL_SOURCE_FILES = raw.FileCount,
            TOTAL_SOURCE_DEFINITIONS = raw.CheatCount,
            PARSED_DEFINITIONS = raw.CheatCount,
            CORPUS_DEFINITIONS = corpus.CheatCount,
            OFFLINE_STATIC_CANDIDATE = raw.StaticCandidates,
            OPTION_SELECTION_REQUIRED = raw.OptionSelectionRequired,
            RUNTIME_EVIDENCE_REQUIRED = raw.RuntimeEvidenceRequired,
            CONSTANT_RUNTIME_REQUIRED = raw.ConstantRuntimeRequired,
            NON_EXECUTABLE_METADATA = raw.NonExecutableMetadata,
            UNSUPPORTED = raw.Unsupported,
            INVALID = raw.Invalid,
            SOURCE_ACQUISITION_GAPS = downloads.Length - raw.FileCount,
            SOURCE_IDENTITY_UNRESOLVED = manifest.Sources.Count(source => source.MatchMode == "Unresolved"),
            EXACT_TARGETS_CHECKED = targets.Length,
            TITLE_ONLY_TARGETS_CHECKED = titleTargets.Length,
            REAL_WPF_WINDOWS_CHECKED = uiTraces.Count,
            RPCS3_RUNNING_CHECK = "SIMULATED_CALLBACK_ONLY_NO_EMULATOR_LAUNCHED",
            SHA256 = pin,
            NEW_CORPUS_PARSE = "PASS", LOOKUP = "PASS", DISCOVERY = "PASS", VM = "PASS", UI = "PASS"
        };
        var json = JsonSerializer.Serialize(summary, JsonOptions);
        File.WriteAllText(Path.Combine(outputDirectory, "source-discovery.summary.json"), json, new UTF8Encoding(false));
        Console.WriteLine(json);
    }

    public static void Regression()
    {
        var root = Path.Combine(Path.GetTempPath(), "NclSourceRegression_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var text =
                "Static\n0\nAuthor\n0 00100000 60000000\n#\n" +
                "Static\n0\nAuthor\n0 00100000 60000000\n#\n" +
                "Option\n0\nAuthor\n0 00100004 0000ZZ\n[ZZ]0001=One;0002=Two[/ZZ]\n#\n" +
                "Runtime Pointer\n0\nAuthor\n01 00100008 00000001\n#\n" +
                "Constant Writer\n1\nAuthor\n0 0010000C 00000001\n#\n" +
                "Metadata\n0\nAuthor-like line\nHuman readable note\n#\n" +
                "Unsupported\n0\nAuthor\n3 00100010 00000001\n#\n" +
                "Invalid Odd Hex\n0\nAuthor\n0 00100010 123\n#\n";
            var staticText = "Extra\n0\nAuthor\n0 00100020 60000000\n#\n";
            var source = Fixture(1, "Discovery Fixture", "Discovery Fixture BLUS12345 01.00.ncl", text);
            var multi = Fixture(2, "Multi Fixture", "Multi Fixture BLES12345 BLUS54321 01.02.ncl", staticText);
            var titleOnly = Fixture(3, "Title Fixture", "Title Fixture 01.00.ncl", staticText + staticText);
            var serialBearing = Fixture(4, "Serial Only", "Serial Only BLES54321 01.00.ncl", staticText);
            var manifest = new Manifest(1, [source, multi, titleOnly, serialBearing]);
            var zip = Path.Combine(root, "fixture.zip");
            WriteFixture(zip, manifest, [text, staticText, staticText + staticText, staticText]);
            var pin = FileSha(zip);
            var corpus = NclCorpusReader.Audit(zip);
            var byPath = corpus.Files.ToDictionary(file => file.RelativePath, StringComparer.Ordinal);
            var main = new Target("BLUS12345", "01.00", "Discovery Fixture");
            var mainLookup = NclCorpusReader.LookupZip(zip, main.Serial, "1.00", pin);
            Assert(mainLookup.IsOk && mainLookup.Cheats.Count == 8,
                "All eight definitions, including identical source occurrences, must survive normalized exact lookup.");
            Assert(mainLookup.Cheats.Count(cheat => cheat.SourceName == "Static") == 2,
                "Manifest discovery must not collapse two same-name same-write source definitions.");
            foreach (var status in Enum.GetValues<NclVerifyStat>())
            {
                Assert(mainLookup.Cheats.Any(cheat => cheat.Status == status),
                    $"Synthetic fixture lost required verifier status {status}.");
            }
            foreach (var candidate in mainLookup.Cheats.Where(cheat => cheat.Status is not
                (NclVerifyStat.OfflineStaticCandidate or NclVerifyStat.OptionSelectionRequired)))
            {
                Assert(!candidate.CanConvert, $"Discovery incorrectly made {candidate.Status} convertible.");
            }
            Assert(NclCorpusReader.LookupZip(zip, main.Serial, "01.01", pin).Cheats.Count == 0,
                "Exact version matching must not widen to another version.");
            foreach (var serial in multi.Serials)
            {
                CheckLookup(zip, pin, manifest, byPath, new Target(serial, "01.02", multi.Title));
            }
            var titleLookup = NclCorpusReader.LookupZip(zip, "BLUS99999", "01.00", pin,
                NclDialect.ArtemisPs3, " title fixture ");
            Assert(titleLookup.Cheats.Count == 2 && titleLookup.Cheats.All(cheat =>
                    !cheat.IdentityProven && !cheat.CanConvert),
                "Exact TitleOnly discovery must preserve both definitions without proving Apply identity.");
            Assert(!NclCorpusReader.ReadZipCandidate(zip, pin, titleLookup.Cheats[0],
                "BLUS99999", "01.00").IsOk, "TitleOnly must fail closed at candidate conversion readback.");
            Assert(NclCorpusReader.LookupZip(zip, "BLUS99999", "01.00", pin,
                NclDialect.ArtemisPs3, "Serial Only").Cheats.Count == 0,
                "Title matching must never override a serial-bearing source.");
            Assert(NclCorpusReader.LookupZip(zip, "BLUS99999", "01.00", pin,
                NclDialect.ArtemisPs3, "Title Fixtur").Cheats.Count == 0,
                "TitleOnly discovery must not use title similarity.");
            CheckLookup(zip, pin, manifest, byPath, main);
            var traces = CheckActualWindows(zip, pin, manifest, byPath,
                [main, new Target(multi.Serials[0], "01.02", multi.Title), ToTarget(titleOnly)]);
            Assert(traces.Any(trace => trace.NonConvertible >= 5 && trace.UiNonConvertible == trace.NonConvertible),
                "The actual ComboBox must retain all nonconvertible verifier classes.");
            var broken = Path.Combine(root, "broken.zip");
            WriteFixture(broken, manifest with { SchemaVersion = 999 },
                [text, staticText, staticText + staticText, staticText]);
            Assert(!NclCorpusReader.LookupZip(broken, main.Serial, main.Version, FileSha(broken)).IsOk,
                "Malformed manifest schema must fail closed.");
            var cases = AppComposition.Build();
            var defaultPinned = new NclManagedPatchUseCase(new BundledCheatPatchService(), cases.PatchMutation, zip);
            Assert(defaultPinned.DiscoverBundled(main.Serial, main.Version).Code == NclCorpusLookupCode.SourceRejected,
                "A nonproduction archive must not pass the default production pin.");
            var explicitPinned = new NclManagedPatchUseCase(new BundledCheatPatchService(), cases.PatchMutation, zip, pin);
            Assert(explicitPinned.DiscoverBundled(main.Serial, main.Version).Cheats.Count == 8,
                "The explicit read-only discovery pin must traverse the real use case.");
            Console.WriteLine(JsonSerializer.Serialize(traces, JsonOptions));
            Console.WriteLine("NclSourceDiscoveryT.Regression: PASS (source occurrences, all statuses, exact/multi/title identity, WPF counts, malformed manifest, pins).");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static TargetTrace CheckLookup(string zip, string pin, Manifest manifest,
        Dictionary<string, NclCorpusFileReport> files, Target target)
    {
        var sources = MatchingSources(manifest, target);
        var expected = sources.SelectMany(source => files[source.EntryPath].Cheats.Select(cheat =>
            source.EntryPath + "/" + cheat.Index.ToString(CultureInfo.InvariantCulture))).Order(StringComparer.Ordinal).ToArray();
        var lookup = NclCorpusReader.LookupZip(zip, target.Serial, target.Version, pin,
            NclDialect.ArtemisPs3, target.Title);
        var actual = lookup.Cheats.SelectMany(cheat => cheat.Sources.Select(source => source.RelativePath + "/" +
            source.CheatIndex.ToString(CultureInfo.InvariantCulture))).Order(StringComparer.Ordinal).ToArray();
        Assert(lookup.IsOk && expected.Length == lookup.Cheats.Count && expected.SequenceEqual(actual, StringComparer.Ordinal),
            $"{target}: source/parser/corpus={expected.Length}, lookup={lookup.Cheats.Count}, occurrences={actual.Length}, code={lookup.Code}.");
        return new TargetTrace(target.Serial, target.Version, target.Title, sources.Length,
            expected.Length, expected.Length, lookup.Cheats.Count, actual.Length);
    }

    private static Source[] MatchingSources(Manifest manifest, Target target) =>
        manifest.Sources.Where(source => source.MatchMode == "TitleOnly"
            ? source.Title.Trim().Equals(target.Title.Trim(), StringComparison.OrdinalIgnoreCase) &&
                (source.Version.Length == 0 || PatVer.Equals(source.Version, target.Version))
            : NclParser.ParseIdentity(source.OriginalFilename).MatchesTarget(target.Serial, PatVer.Norm(target.Version)!)).ToArray();

    private static List<Trace> CheckActualWindows(string zip, string pin, Manifest manifest,
        Dictionary<string, NclCorpusFileReport> files, Target[] targets)
    {
        var traces = new List<Trace>();
        Exception? error = null;
        var previousLanguage = UiText.CurLang;
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            var fakeRoot = Path.Combine(Path.GetTempPath(), "NclSourceReadOnly_" + Guid.NewGuid().ToString("N"));
            try
            {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
                var app = new R3Config.App();
                app.InitializeComponent();
                app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                var cases = AppComposition.Build();
                var flow = new AppFlow(cases with { NclManagedPatch = new NclManagedPatchUseCase(
                    new BundledCheatPatchService(), cases.PatchMutation, zip, pin) });
                var install = new Rpcs3InstallDescriptor(fakeRoot, Path.Combine(fakeRoot, "rpcs3.exe"),
                    Path.Combine(fakeRoot, "config"), Path.Combine(fakeRoot, "custom_configs"),
                    Path.Combine(fakeRoot, "patches"), Path.Combine(fakeRoot, "patch_config.yml"));
                foreach (var language in new[] { UiText.EnLang, UiText.ArabLang })
                {
                    UiText.SetLang(language);
                    UiLaySvc.Apply();
                    foreach (var target in targets)
                    {
                        var expected = CheckLookup(zip, pin, manifest, files, target);
                        var running = false;
                        var game = new Rpcs3GameDescriptor(target.Serial, target.Title, string.Empty,
                            target.Version, null, null);
                        var vm = new NclImportVm(flow, install, game, game.Title, [], () => running,
                            (_, _) => false, () => null);
                        Pump(dispatcher, vm.InitializeAsync());
                        var window = new NclImportDlg(null, vm);
                        try
                        {
                            window.Show();
                            window.UpdateLayout();
                            dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
                            var combo = Walk<ComboBox>(window).Single(control => ReferenceEquals(control.ItemsSource, vm.Cheats));
                            combo.IsDropDownOpen = true;
                            combo.UpdateLayout();
                            dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
                            Assert(combo.IsVisible && combo.IsDropDownOpen && combo.Items.Count == vm.Cheats.Count &&
                                vm.Cheats.Count == expected.Lookup && combo.Items.Cast<NclManagedPatchCheatChoice>()
                                    .SequenceEqual(vm.Cheats), "Actual NCL dialog selector lost or replaced VM definitions.");
                            var hidden = vm.Cheats.Count(cheat => !cheat.CanConvert);
                            var displayed = combo.Items.Cast<NclManagedPatchCheatChoice>().Count(cheat => !cheat.CanConvert);
                            var identities = vm.Cheats.Select(ChoiceKey).ToArray();
                            running = true;
                            // Re-enter the actual bundled discovery command on the same VM.
                            vm.SelectedSource = vm.Sources.Single(source => source.IsExternal);
                            vm.UseBundledCmd.Execute(null);
                            Pump(dispatcher, vm.CurrentOperation);
                            window.UpdateLayout();
                            dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
                            Assert(identities.SequenceEqual(vm.Cheats.Select(ChoiceKey), StringComparer.Ordinal) &&
                                combo.Items.Count == expected.Lookup && !Directory.Exists(fakeRoot),
                                "Simulated RPCS3 running state changed discovery or created RPCS3 files.");
                            traces.Add(new Trace(target.Serial, target.Version, target.Title, language,
                                expected.Parser, expected.Parser, expected.Corpus, expected.Lookup,
                                vm.Cheats.Count, combo.Items.Count, hidden, displayed, combo.Items.Count,
                                !Directory.Exists(fakeRoot)));
                            combo.IsDropDownOpen = false;
                        }
                        finally
                        {
                            window.Close();
                        }
                    }
                }
            }
            catch (Exception failure)
            {
                error = failure;
            }
            finally
            {
                UiText.SetLang(previousLanguage);
                System.Windows.Application.Current?.Shutdown();
                dispatcher.InvokeShutdown();
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromMinutes(3)))
        {
            throw new TimeoutException("NCL source WPF acceptance exceeded three minutes.");
        }
        if (error is not null)
        {
            throw new InvalidOperationException("NCL source WPF acceptance failed.", error);
        }
        return traces;
    }

    private static IEnumerable<T> Walk<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T match)
        {
            yield return match;
        }
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            foreach (var child in Walk<T>(VisualTreeHelper.GetChild(root, index)))
            {
                yield return child;
            }
        }
    }

    private static void Pump(Dispatcher dispatcher, Task task)
    {
        if (!task.IsCompleted)
        {
            var frame = new DispatcherFrame();
            _ = task.ContinueWith(_ => dispatcher.BeginInvoke(new Action(() => frame.Continue = false)), TaskScheduler.Default);
            Dispatcher.PushFrame(frame);
        }
        task.GetAwaiter().GetResult();
    }

    private static Manifest ReadManifest(string zip)
    {
        using var archive = ZipFile.OpenRead(zip);
        using var stream = archive.GetEntry("manifest.json")!.Open();
        return JsonSerializer.Deserialize<Manifest>(stream) ?? throw new InvalidDataException("Candidate manifest empty.");
    }

    private static Source Fixture(int row, string title, string filename, string text)
    {
        var identity = NclParser.ParseIdentity(filename);
        return new Source(row, title, [.. identity.Serials],
            identity.Versions.Count > 0 ? identity.Versions[0] : "01.00",
            identity.Serials.Count == 0 ? "TitleOnly" : "ExactSerialVersion", filename,
            Sha(Encoding.UTF8.GetBytes(text)), "codes/site/" + row.ToString("D4", CultureInfo.InvariantCulture) + "/" + filename);
    }

    private static void WriteFixture(string path, Manifest manifest, string[] texts)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        for (var index = 0; index < manifest.Sources.Length; index++)
        {
            using var stream = archive.CreateEntry(manifest.Sources[index].EntryPath).Open();
            stream.Write(Encoding.UTF8.GetBytes(texts[index]));
        }
        using var metadata = archive.CreateEntry("manifest.json").Open();
        JsonSerializer.Serialize(metadata, manifest);
    }

    private static Target ToTarget(Source source)
    {
        var targets = NclParser.ParseIdentity(source.OriginalFilename).Targets;
        return targets.Count == 0 ? new Target("BLUS99999", PatVer.Norm(source.Version) ?? "01.00", source.Title)
            : new Target(targets[0].Serial, targets[0].Version, source.Title);
    }

    private static string CheatKey(NclCorpusCheatReport cheat) =>
        $"{cheat.Index}|{cheat.Name}|{cheat.Status}|{cheat.RawSha256}";
    private static string ChoiceKey(NclManagedPatchCheatChoice choice) =>
        $"{choice.BundledCandidate!.PrimarySource.RelativePath}|{choice.Index}|{choice.Name}|{choice.Status}";
    private static string Sha(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static string FileSha(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
