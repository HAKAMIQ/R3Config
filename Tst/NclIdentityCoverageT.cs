using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Threading;
using R3Config.Mdl;
using R3Config.Svc;
using R3Config.Vm;

namespace R3Config.Tests;

// Read-only acceptance of the complete HTML dataset against a candidate ZIP.
// The expectation oracle reads the two raw identity cells together, independently
// of NclParser; the production parser never supplies its own expected targets.
internal static class NclIdentityCoverageT
{
    private static readonly Regex RowPattern = new(
        @"<tr><td>(.*?)<td>(.*?)<td>(.*?)<td>.*?</tr>",
        RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5));
    private static readonly Regex SerialPattern = new(
        @"(?<![A-Z0-9])[A-Z]{4}[0-9]{5}(?![A-Z0-9])",
        RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static readonly Regex WordPattern = new(
        @"[A-Za-z0-9]+(?:\.[A-Za-z0-9]+)?",
        RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static readonly Regex VersionPattern = new(
        @"^(?<kind>av|v)?(?<version>[0-9]{1,2}\.[0-9]{1,2})$",
        RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private sealed record SourceEntry(int Row, string Title, string TitleId, string Version, string FileName);
    private sealed record ExpectedBinding(string Serial, string? SourceVersion, string? AppVersion)
    {
        public string Version => AppVersion ?? SourceVersion!;
        public string Key => Serial + "/" + Version;
    }
    private sealed record ReachableBinding(string Serial, string Version, int LookupCheats, int UiCheats);
    private sealed record EntryReport(
        SourceEntry SOURCE_ENTRY,
        IReadOnlyList<NclTargetBinding> EXTRACTED_BINDINGS,
        IReadOnlyList<string> UNRESOLVED_REASON,
        IReadOnlyList<ExpectedBinding> INDEPENDENT_PROVEN_BINDINGS,
        IReadOnlyList<ReachableBinding> LOOKUP_REACHABLE_BINDINGS,
        IReadOnlyList<string> MISSING_BINDINGS);
    private sealed record TargetObservation(NclCorpusLookupResult Lookup, NclCorpusCheatCandidate[] UiCheats);

    public static void Run(string datasetHtml, string candidateZip, string outputDirectory)
    {
        var html = R3Bound.ReadUtf8(datasetHtml, 4L * 1024 * 1024, "Artemis dataset HTML");
        var rows = ReadRows(html);
        TestAssert.Assert(rows.Count == 2501, $"Expected the complete 2501-entry dataset; found {rows.Count}.");
        var corpus = NclCorpusReader.Audit(candidateZip);
        var byName = corpus.Files.GroupBy(file => Path.GetFileName(file.RelativePath), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var pin = corpus.SourceSha256 ?? throw new InvalidDataException("Candidate ZIP has no SHA-256.");
        // NclManagedPatchUseCase pins the bundled corpus to one compile-time
        // SHA-256, so the use case and the view model can only ever read the
        // shipped App/Data/ncl.zip. Against any other archive the UI stage is
        // declared unexercised rather than faked with a relaxed pin, which
        // would be a production change made to satisfy a test.
        var uiPinned = pin.Equals(
            R3Config.Application.NclManagedPatchUseCase.BundledCorpusSha256,
            StringComparison.OrdinalIgnoreCase);
        var cache = new Dictionary<string, TargetObservation>(StringComparer.Ordinal);
        var reports = new List<EntryReport>(rows.Count);
        var gates = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["VALID_SERIAL_BINDINGS_SILENTLY_LOST"] = 0,
            ["MULTISERIAL_BINDINGS_LOST"] = 0,
            ["VERSION_BINDINGS_LOST"] = 0,
            ["LOOKUP_ZERO_FOR_PROVEN_BINDING"] = 0,
            ["UI_ZERO_FOR_LOOKUP_BINDING"] = 0
        };
        var missingSources = 0;
        var semanticIneligible = 0;
        var provenBindings = 0;
        var eligibleBindings = 0;

        RunSta((dispatcher, flow, install) =>
        {
            foreach (var row in rows)
            {
                var identity = NclParser.ParseIdentity(row.FileName);
                var segment = row.TitleId + " " + row.Version;
                var serials = SerialPattern.Matches(segment).Select(match => match.Value)
                    .Distinct(StringComparer.Ordinal).ToArray();
                var expected = ProvenBindings(segment);
                provenBindings += expected.Count;
                var missing = new List<string>();
                var unresolved = new List<string>();
                var reachable = new List<ReachableBinding>();
                if (identity.UnresolvedReason is not null)
                {
                    unresolved.Add(identity.UnresolvedReason);
                }
                unresolved.AddRange(identity.TargetBindings.Where(binding => binding.UnresolvedReason is not null)
                    .Select(binding => binding.Serial + ":" + binding.UnresolvedReason));
                foreach (var serial in serials)
                {
                    if (!identity.TargetBindings.Any(binding => binding.Serial == serial))
                    {
                        gates["VALID_SERIAL_BINDINGS_SILENTLY_LOST"]++;
                        if (serials.Length > 1)
                        {
                            gates["MULTISERIAL_BINDINGS_LOST"]++;
                        }
                        missing.Add(serial + ":SERIAL_NOT_EXTRACTED");
                    }
                }

                byName.TryGetValue(row.FileName, out var sourceFiles);
                if (sourceFiles is null)
                {
                    missingSources++;
                    unresolved.Add("SOURCE_ENTRY_MISSING_FROM_CANDIDATE");
                    missing.Add(row.FileName + ":SOURCE_ENTRY_MISSING");
                }

                foreach (var target in expected)
                {
                    if (!identity.TargetBindings.Any(binding => binding.Serial == target.Serial &&
                            binding.SourceVersion == target.SourceVersion && binding.AppVersion == target.AppVersion &&
                            binding.EffectiveVersion == target.Version))
                    {
                        gates["VERSION_BINDINGS_LOST"]++;
                        missing.Add(target.Key + ":VERSION_NOT_EXTRACTED");
                    }

                    var observation = Observe(target, dispatcher, flow, install);
                    // Source provenance makes this an entry-specific assertion:
                    // another file for the same game cannot conceal a lost file.
                    var sourceCandidates = observation.Lookup.Cheats.Where(candidate => candidate.Sources.Any(source =>
                        Path.GetFileName(source.RelativePath).Equals(row.FileName, StringComparison.Ordinal))).ToArray();
                    var convertible = sourceCandidates.Length > 0
                        ? sourceCandidates.Any(candidate => candidate.CanConvert)
                        : sourceFiles?.Any(file => file.Cheats.Any(cheat =>
                            (cheat.Status == NclVerifyStat.OfflineStaticCandidate &&
                             cheat.SemanticSha256 is not null) ||
                            cheat.Status == NclVerifyStat.OptionSelectionRequired)) == true;
                    if (convertible)
                    {
                        eligibleBindings++;
                    }
                    else if (sourceFiles is not null)
                    {
                        semanticIneligible++;
                        unresolved.Add(target.Key + ":SEMANTIC_INELIGIBLE_DISCOVERY_ONLY");
                    }

                    // Convertibility must never decide discovery. Every exact,
                    // independently proven Serial+Version binding with a source
                    // cheat must reach Lookup/UI even when Preview is disabled.
                    if (sourceCandidates.Length == 0)
                    {
                        gates["LOOKUP_ZERO_FOR_PROVEN_BINDING"]++;
                        missing.Add(target.Key + ":LOOKUP_ZERO:" + observation.Lookup.Code);
                        continue;
                    }

                    var visible = observation.UiCheats.Count(candidate => sourceCandidates.Any(expectedCandidate =>
                        CandidateKey(candidate) == CandidateKey(expectedCandidate)));
                    if (uiPinned && visible != sourceCandidates.Length)
                    {
                        gates["UI_ZERO_FOR_LOOKUP_BINDING"]++;
                        missing.Add(target.Key + ":UI_MISSING_CHEATS");
                    }
                    reachable.Add(new ReachableBinding(target.Serial, target.Version, sourceCandidates.Length, visible));
                }

                reports.Add(new EntryReport(row, identity.TargetBindings, unresolved.Distinct(StringComparer.Ordinal).ToArray(),
                    expected, reachable, missing));
                if (row.Row % 100 == 0)
                {
                    Console.WriteLine($"NCL identity coverage: {row.Row}/{rows.Count} entries, {cache.Count} actual lookup/UI targets.");
                }
            }
        }, candidateZip);

        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(Path.Combine(outputDirectory, "identity-coverage.entries.json"),
            JsonSerializer.Serialize(reports, JsonOptions), new UTF8Encoding(false));
        var tsv = new StringBuilder("SOURCE_ENTRY\tEXTRACTED_BINDINGS\tUNRESOLVED_REASON\tLOOKUP_REACHABLE_BINDINGS\tMISSING_BINDINGS\n");
        foreach (var row in reports)
        {
            tsv.Append(Tsv(row.SOURCE_ENTRY.FileName)).Append('\t')
                .Append(Tsv(JsonSerializer.Serialize(row.EXTRACTED_BINDINGS))).Append('\t')
                .Append(Tsv(string.Join(";", row.UNRESOLVED_REASON))).Append('\t')
                .Append(Tsv(JsonSerializer.Serialize(row.LOOKUP_REACHABLE_BINDINGS))).Append('\t')
                .Append(Tsv(string.Join(";", row.MISSING_BINDINGS))).Append('\n');
        }
        File.WriteAllText(Path.Combine(outputDirectory, "identity-coverage.entries.tsv"), tsv.ToString(), new UTF8Encoding(false));
        var summary = new
        {
            DatasetSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(html))),
            CandidateSha256 = pin,
            DatasetEntries = rows.Count,
            CandidateFiles = corpus.FileCount,
            CandidateCheats = corpus.CheatCount,
            CandidateTargetBindings = corpus.Files.Sum(file => file.Identity.TargetBindings.Count),
            CandidateProvenTargetBindings = corpus.Files.Sum(file => file.Identity.Targets.Count),
            ExtractedBindings = reports.Sum(row => row.EXTRACTED_BINDINGS.Count),
            IndependentlyProvenBindings = provenBindings,
            EligibleProvenBindings = eligibleBindings,
            SemanticIneligibleBindings = semanticIneligible,
            LookupReachableBindings = reports.Sum(row => row.LOOKUP_REACHABLE_BINDINGS.Count),
            UiStage = uiPinned
                ? "EXERCISED"
                : "NOT_EXERCISED_CORPUS_NOT_PINNED",
            UiReachableBindings = reports.Sum(row => row.LOOKUP_REACHABLE_BINDINGS.Count(binding => binding.UiCheats > 0)),
            ActualUniqueLookupAndUiTargets = cache.Count,
            UnresolvedEntries = reports.Count(row => row.UNRESOLVED_REASON.Count > 0),
            UnresolvedNoSerial = reports.Count(row => row.UNRESOLVED_REASON.Contains("UNRESOLVED_NO_SERIAL", StringComparer.Ordinal)),
            MissingSourceEntries = missingSources,
            MissingBindings = reports.Sum(row => row.MISSING_BINDINGS.Count),
            Gates = gates,
            Passed = missingSources == 0 && gates.Values.All(value => value == 0)
        };
        var summaryText = JsonSerializer.Serialize(summary, JsonOptions);
        File.WriteAllText(Path.Combine(outputDirectory, "identity-coverage.summary.json"), summaryText, new UTF8Encoding(false));
        Console.WriteLine(summaryText);
        TestAssert.Assert(summary.Passed, "NCL identity coverage gates failed; inspect identity-coverage.entries.json.");

        TargetObservation Observe(ExpectedBinding target, Dispatcher dispatcher, AppFlow flow, Rpcs3InstallDescriptor install)
        {
            if (cache.TryGetValue(target.Key, out var observed))
            {
                return observed;
            }
            var lookup = NclCorpusReader.LookupZip(candidateZip, target.Serial, target.Version, pin);
            var game = new Rpcs3GameDescriptor(target.Serial, "Identity coverage fixture", string.Empty,
                target.Version, null, null);
            NclCorpusCheatCandidate[] uiCandidates = [];

            if (uiPinned)
            {
                var vm = new NclImportVm(flow, install, game, game.Title, [], () => false, (_, _) => false, () => null);
                Pump(dispatcher, vm.InitializeAsync());
                uiCandidates =
                    [.. vm.Cheats.Where(cheat => cheat.BundledCandidate is not null).Select(cheat => cheat.BundledCandidate!)];
            }

            observed = new TargetObservation(lookup, uiCandidates);
            cache.Add(target.Key, observed);
            return observed;
        }
    }

    private static List<SourceEntry> ReadRows(string html)
    {
        var rows = new List<SourceEntry>();
        foreach (Match match in RowPattern.Matches(html))
        {
            var titleId = Unescape(match.Groups[1].Value);
            var title = Unescape(match.Groups[2].Value);
            var version = Unescape(match.Groups[3].Value);
            var name = title;

            if (titleId.Length > 0)
            {
                name += " " + titleId;
            }

            // A row with no TitleID keeps its version inside the name cell and
            // repeats it in the version cell, so appending it again builds a
            // filename that never existed. The test is exact suffix equality on
            // the version cell: a name ending in a different version, or one
            // whose source filename genuinely repeats a version after a serial,
            // is untouched.
            if (version.Length > 0 &&
                !name.EndsWith(" " + version, StringComparison.Ordinal) &&
                !name.Equals(version, StringComparison.Ordinal))
            {
                name += " " + version;
            }

            rows.Add(new SourceEntry(
                rows.Count + 1, title, titleId, version, name + ".ncl"));
        }
        return rows;
    }

    private static string Unescape(string value)
    {
        return value.Replace("&amp;", "&", StringComparison.Ordinal);
    }

    private static List<ExpectedBinding> ProvenBindings(string identitySegment)
    {
        var words = WordPattern.Matches(identitySegment.Replace("&amp;", "&", StringComparison.Ordinal))
            .Select(match => match.Value).ToArray();
        var result = new List<ExpectedBinding>();
        for (var index = 0; index < words.Length;)
        {
            if (!IsSerial(words[index]))
            {
                index++;
                continue;
            }
            var serials = new List<string>();
            while (index < words.Length && IsSerial(words[index]))
            {
                serials.Add(words[index++]);
            }
            var sources = new HashSet<string>(StringComparer.Ordinal);
            var apps = new HashSet<string>(StringComparer.Ordinal);
            while (index < words.Length)
            {
                var version = VersionPattern.Match(words[index]);
                if (!version.Success)
                {
                    break;
                }
                var normalizedVersion = NormalizeVersion(
                    version.Groups["version"].Value);
                (version.Groups["kind"].Value == "av" ? apps : sources)
                    .Add(normalizedVersion);
                index++;
            }
            // More than one distinct source or app version requires pairing
            // evidence this conservative independent oracle does not invent.
            if (sources.Count <= 1 && apps.Count <= 1 && sources.Count + apps.Count > 0)
            {
                foreach (var serial in serials.Distinct(StringComparer.Ordinal))
                {
                    result.Add(new ExpectedBinding(serial, sources.SingleOrDefault(), apps.SingleOrDefault()));
                }
            }
        }
        return result.Distinct().ToList();
    }

    private static bool IsSerial(string token) => token.Length == 9 && SerialPattern.IsMatch(token);

    // Keep the independent website oracle on the same textual version domain
    // without asking NclParser to tell the test what to expect. Artemis rows
    // legitimately contain both 1.00 and 01.00; both denote the same exact
    // application version and must compare against the normalized production
    // binding as 01.00.
    private static string NormalizeVersion(string value)
    {
        var parts = value.Split('.', StringSplitOptions.None);
        if (parts.Length != 2 ||
            !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var major) ||
            !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minor) ||
            major is < 0 or > 99 ||
            minor is < 0 or > 99)
        {
            throw new InvalidDataException($"Invalid independently parsed Artemis version: {value}");
        }

        return major.ToString("00", CultureInfo.InvariantCulture) + "." +
               minor.ToString("00", CultureInfo.InvariantCulture);
    }

    private static string CandidateKey(NclCorpusCheatCandidate candidate) =>
        candidate.Name + "/" + candidate.Status + "/" + candidate.SemanticSha256;
    private static string Tsv(string value) => value.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');

    private static void RunSta(
        Action<Dispatcher, AppFlow, Rpcs3InstallDescriptor> body,
        string candidateZip)
    {
        Exception? failure = null;
        var root = Path.Combine(Path.GetTempPath(), "NclIdentityReadOnly_" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            try
            {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
                var cases = R3Config.Composition.AppComposition.Build();
                var flow = new AppFlow(cases with
                {
                    NclManagedPatch = new R3Config.Application.NclManagedPatchUseCase(
                        new BundledCheatPatchService(), cases.PatchMutation, candidateZip)
                });
                var install = new Rpcs3InstallDescriptor(root, Path.Combine(root, "rpcs3.exe"),
                    Path.Combine(root, "config"), Path.Combine(root, "custom_configs"),
                    Path.Combine(root, "patches"), Path.Combine(root, "patch_config.yml"));
                body(dispatcher, flow, install);
                TestAssert.Assert(!Directory.Exists(root), "Read-only identity lookup/UI initialization created its fake RPCS3 root.");
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                dispatcher.InvokeShutdown();
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null)
        {
            throw new InvalidOperationException("NCL identity lookup/UI coverage failed.", failure);
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
}
