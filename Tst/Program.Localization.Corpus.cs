using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;
using R3Config.Vm;

internal static partial class Program
{
    static void PatchText_RequestedGameFamiliesHaveArabicCoverage()
    {
        var catPath = Path.Combine(
            RepoRoot(),
            "App",
            "Data",
            "cat.json");
        using var document = JsonDocument.Parse(
            File.ReadAllText(catPath));

        (string Label, Func<string, bool> Matches)[] families =
        [
            ("Demon's Souls", title => title.Equals("Demon's Souls", StringComparison.OrdinalIgnoreCase)),
            ("God of War III", title => title.Equals("God of War III", StringComparison.OrdinalIgnoreCase)),
            ("The Last of Us", title => title.Equals("The Last of Us", StringComparison.OrdinalIgnoreCase)),
            ("Uncharted 2", title => title.StartsWith("Uncharted 2", StringComparison.OrdinalIgnoreCase)),
            ("Uncharted 3", title => title.StartsWith("Uncharted 3", StringComparison.OrdinalIgnoreCase)),
            ("inFamous", title => title.StartsWith("inFamous", StringComparison.OrdinalIgnoreCase)),
            ("Metal Gear Solid 4", title => title.StartsWith("Metal Gear Solid 4", StringComparison.OrdinalIgnoreCase)),
            ("Persona 5", title => title.Equals("Persona 5", StringComparison.OrdinalIgnoreCase)),
            ("Ratchet & Clank", title => title.Contains("Ratchet & Clank", StringComparison.OrdinalIgnoreCase)),
            ("Battlefield", title => title.StartsWith("Battlefield", StringComparison.OrdinalIgnoreCase)),
            ("Call of Duty", title => title.StartsWith("Call of Duty", StringComparison.OrdinalIgnoreCase)),
            ("Sonic", title => title.StartsWith("Sonic", StringComparison.OrdinalIgnoreCase)),
            ("Minecraft", title => title.StartsWith("Minecraft", StringComparison.OrdinalIgnoreCase))
        ];

        var recommendations = document.RootElement
            .GetProperty("Recommendations")
            .EnumerateArray()
            .ToArray();

        PatchText_WithLang(
            UiText.ArabLang,
            () =>
            {
                foreach (var family in families)
                {
                    var realNotes = recommendations
                        .Where(recommendation => family.Matches(
                            recommendation.GetProperty("WikiTitle").GetString()
                            ?? string.Empty))
                        .SelectMany(recommendation => recommendation
                            .GetProperty("Changes")
                            .EnumerateArray())
                        .Where(change =>
                            change.TryGetProperty("Notes", out var note) &&
                            note.ValueKind == JsonValueKind.String &&
                            !string.IsNullOrWhiteSpace(note.GetString()))
                        .Select(change => change
                            .GetProperty("Notes")
                            .GetString()!)
                        .ToArray();

                    Assert(realNotes.Length > 0,
                        $"Expected real non-empty Wiki Notes for {family.Label}.");
                    foreach (var raw in realNotes)
                    {
                        var localized = NoteSvc.Trans(raw);
                        Assert(localized.Quality != NoteQual.Fallback &&
                               !string.IsNullOrWhiteSpace(localized.Body) &&
                               NoteSvc.ContainsArabic(localized.Body) &&
                               localized.Body != raw,
                            $"Requested regression family lacks actual Arabic Note coverage: {family.Label} :: {raw}");
                    }
                }
            });
    }

    static void PatchText_CorpusWideSafety()
    {
        var repoRoot = RepoRoot();
        var dataRoot = Path.Combine(repoRoot, "App", "Data");
        var artPath = Path.Combine(dataRoot, "art.yml");
        var canPath = Path.Combine(dataRoot, "can.yml");
        var catPath = Path.Combine(dataRoot, "cat.json");

        var reader = new R3Config.Svc.ReadSvc();
        var artRead = reader.Read(artPath, PatSrc.Artemis);
        var canRead = reader.Read(canPath, PatSrc.Can);

        Assert(artRead.IsOk && artRead.Catalog.Entries.Count == 1023,
            "Expected the complete bundled Artemis corpus before localization audit.");
        Assert(canRead.IsOk && canRead.Catalog.Entries.Count > 0,
            "Expected the complete bundled Canary corpus before localization audit.");

        var notes = new List<(string Source, string Title, string Raw)>();
        var totalNoteSlots =
            artRead.Catalog.Entries.Count +
            canRead.Catalog.Entries.Count;
        foreach (var entry in artRead.Catalog.Entries)
        {
            if (!string.IsNullOrWhiteSpace(entry.Notes))
            {
                notes.Add(("Artemis", entry.Title, entry.Notes!));
            }
        }

        foreach (var entry in canRead.Catalog.Entries)
        {
            if (!string.IsNullOrWhiteSpace(entry.Notes))
            {
                notes.Add(("Canary", entry.Title, entry.Notes!));
            }
        }

        var officialChangeCount = 0;
        using (var document = JsonDocument.Parse(File.ReadAllText(catPath)))
        {
            var recommendations = document.RootElement.GetProperty("Recommendations");
            foreach (var recommendation in recommendations.EnumerateArray())
            {
                foreach (var change in recommendation.GetProperty("Changes").EnumerateArray())
                {
                    officialChangeCount++;
                    var title = change.TryGetProperty("Key", out var key)
                        ? key.GetString() ?? string.Empty
                        : string.Empty;
                    if (change.TryGetProperty("Notes", out var note) &&
                        note.ValueKind == JsonValueKind.String &&
                        !string.IsNullOrWhiteSpace(note.GetString()))
                    {
                        notes.Add(("RPCS3 Wiki", title, note.GetString()!));
                    }
                }
            }
        }

        totalNoteSlots += officialChangeCount;

        Assert(officialChangeCount >= 7000,
            "Expected the complete bundled RPCS3 Wiki recommendation corpus before localization audit.");

        var emptySourceNotes = totalNoteSlots - notes.Count;
        var uniqueNotes = notes
            .Select(note => note.Raw.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        Assert(totalNoteSlots == 8601 &&
               notes.Count == 7666 &&
               emptySourceNotes == 935 &&
               uniqueNotes.Length == 835,
            $"Unexpected Note accounting. Total={totalNoteSlots}, NonEmpty={notes.Count}, Empty={emptySourceNotes}, Unique={uniqueNotes.Length}.");

        PatchText_WithLang(
            UiText.EnLang,
            () =>
            {
                foreach (var (source, title, raw) in notes)
                {
                    var result = NoteSvc.Trans(raw);
                    Assert(result.Body == raw.Trim() &&
                           result.Quality == NoteQual.Original &&
                           !result.HasOrig,
                        $"{source} English presentation changed the canonical source Note: {title}");
                }
            });

        static bool HasArabicDiacritic(string value) =>
            value.Any(ch =>
                (ch >= '\u0610' && ch <= '\u061A') ||
                (ch >= '\u064B' && ch <= '\u065F') ||
                ch == '\u0670' ||
                (ch >= '\u06D6' && ch <= '\u06ED'));

        PatchText_WithLang(
            UiText.ArabLang,
            () =>
            {
                foreach (var entry in artRead.Catalog.Entries.Concat(canRead.Catalog.Entries))
                {
                    Assert(PatText.DspTitle(entry.Title) == entry.Title,
                        "Corpus localization changed a canonical patch title: " + entry.Title);
                }

                var exact = 0;
                var structured = 0;
                var fallback = 0;
                var omitted = 0;
                var instructionCount = 0;
                var actualTranslations = 0;
                var exactUnique = new HashSet<string>(StringComparer.Ordinal);
                var structuredUnique = new HashSet<string>(StringComparer.Ordinal);

                var productionText = string.Join(
                    Environment.NewLine,
                    Directory.EnumerateFiles(
                            Path.Combine(repoRoot, "App"),
                            "*.*",
                            SearchOption.AllDirectories)
                        .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                                       path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
                                       path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
                        .Where(path => !path.Contains(
                            Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar,
                            StringComparison.OrdinalIgnoreCase) &&
                                       !path.Contains(
                            Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar,
                            StringComparison.OrdinalIgnoreCase))
                        .Select(File.ReadAllText));
                Assert(ForbiddenTranslationPhrases.All(phrase =>
                           !productionText.Contains(
                               phrase,
                               StringComparison.OrdinalIgnoreCase)),
                    "A translation-unavailable product fallback remains in App sources or resources.");

                foreach (var (source, title, raw) in notes)
                {
                    var result = NoteSvc.Trans(raw);

                    if (!NoteSvc.ContainsArabic(raw))
                    {
                        Assert(!string.Equals(result.Body, raw, StringComparison.Ordinal),
                            $"{source} raw English note leaked into Arabic primary UI: {title}");
                    }

                    Assert(!result.Body.Contains("HASH_UNKNOWN", StringComparison.OrdinalIgnoreCase) &&
                           !result.Body.Contains("HASHMISMATCH", StringComparison.OrdinalIgnoreCase) &&
                           !result.Body.Contains("HASH_MISMATCH", StringComparison.OrdinalIgnoreCase),
                        $"{source} internal diagnostic leaked into Arabic primary UI: {title}");

                    Assert(!HasArabicDiacritic(result.Body),
                        $"{source} localized patch text contains Arabic diacritics: {title}");

                    Assert(!string.IsNullOrWhiteSpace(result.Body) &&
                           (NoteSvc.ContainsArabic(result.Body) || NoteSvc.ContainsArabic(raw)),
                        $"{source} non-empty Note has no actual Arabic presentation: {title} :: {raw}");
                    Assert(result.Quality != NoteQual.Fallback,
                        $"{source} non-empty Note reached fallback instead of reviewed Arabic: {title} :: {raw}");
                    Assert(ForbiddenTranslationPhrases.All(phrase =>
                               !result.Body.Contains(
                                   phrase,
                                   StringComparison.OrdinalIgnoreCase)),
                        $"{source} Arabic Note exposed a forbidden fallback phrase: {title}");

                    var scaffoldProbe = result.Body
                        .Replace("Split-Screen", string.Empty, StringComparison.OrdinalIgnoreCase)
                        .Replace("Allow Host GPU Labels", string.Empty, StringComparison.OrdinalIgnoreCase);
                    foreach (var protectedTerm in NoteSvc.ProtectedRpcS3Terms
                                 .Concat(NoteSvc.ProtectedRpcS3Values))
                    {
                        scaffoldProbe = scaffoldProbe.Replace(
                            protectedTerm,
                            string.Empty,
                            StringComparison.OrdinalIgnoreCase);
                    }

                    Assert(!UntranslatedNoteScaffoldRegex().IsMatch(scaffoldProbe),
                        $"{source} Arabic Note leaked an untranslated English scaffold: {title} :: {result.Body}");

                    Assert(CorpusNumericTokens(raw).SequenceEqual(
                               CorpusNumericTokens(result.Body),
                               StringComparer.OrdinalIgnoreCase),
                        $"{source} Arabic Note changed a numeric/FPS/Hz token: {title} :: {raw} :: {result.Body}");

                    foreach (var technicalToken in CorpusTechnicalTokens(raw))
                    {
                        Assert(result.Body.Contains(
                                   technicalToken,
                                   StringComparison.Ordinal),
                            $"{source} Arabic Note changed technical token '{technicalToken}': {title}");
                    }

                    Assert(PreservesSemanticForce(raw, result.Body),
                        $"{source} Arabic Note lost a requirement, condition, warning, or side effect: {title} :: {raw} :: {result.Body}");

                    foreach (var protectedTerm in NoteSvc.ProtectedRpcS3Terms)
                    {
                        if (protectedTerm.Contains(' ') &&
                            CorpusContainsProtectedTerm(
                                raw,
                                protectedTerm))
                        {
                            Assert(result.Body.Contains(
                                       protectedTerm,
                                       StringComparison.Ordinal),
                                $"{source} Arabic Note changed protected RPCS3 term '{protectedTerm}': {title}");
                        }
                    }

                    actualTranslations++;

                    if (result.Kind is NoteKind.Instruction or NoteKind.Requirement or NoteKind.Warning)
                    {
                        instructionCount++;
                        Assert(!string.IsNullOrWhiteSpace(result.Body),
                            $"{source} actionable/warning note disappeared in Arabic UI: {title} :: {raw}");
                    }

                    if (result.HasOrig)
                    {
                        Assert(result.OrigBody == raw.Trim(),
                            $"{source} secondary original note was not preserved: {title}");
                    }

                    switch (result.Quality)
                    {
                        case NoteQual.Exact:
                            exact++;
                            exactUnique.Add(raw.Trim());
                            break;
                        case NoteQual.Fragment:
                            structured++;
                            structuredUnique.Add(raw.Trim());
                            break;
                        case NoteQual.Fallback when string.IsNullOrWhiteSpace(result.Body):
                            omitted++;
                            break;
                        case NoteQual.Fallback:
                            fallback++;
                            break;
                    }
                }

                Assert(actualTranslations == notes.Count &&
                       fallback == 0 &&
                       omitted == 0 &&
                       exactUnique.Count == 834 &&
                       structuredUnique.Count == 1 &&
                       NoteSvc.ReviewedWikiArabicTranslationCount == 612,
                    $"Arabic corpus coverage is incomplete. Translated={actualTranslations}, Fallback={fallback}, Omitted={omitted}, NonEmpty={notes.Count}.");

                var emptyResult = NoteSvc.Trans(null);
                var emptyPresentation = PatText.PatMsg("   ");
                Assert(string.IsNullOrEmpty(emptyResult.Body) &&
                       string.IsNullOrEmpty(emptyPresentation.Body),
                    "Empty source Notes must not produce a placeholder or fallback surface.");

                Console.WriteLine(
                    $"Patch localization corpus: Artemis={artRead.Catalog.Entries.Count}, " +
                    $"Canary={canRead.Catalog.Entries.Count}, WikiChanges={officialChangeCount}, " +
                    $"TotalNoteSlots={totalNoteSlots}, EmptyNoteSlots={emptySourceNotes}, " +
                    $"NonEmptyNoteOccurrences={notes.Count}, UniqueNonEmptySourceNotes={uniqueNotes.Length}, " +
                    $"ExactOccurrences={exact}, UniqueArabicExactTranslations={exactUnique.Count}, " +
                    $"ReviewedPatchExactDictionary={NoteSvc.ReviewedPatchArabicTranslationCount}, " +
                    $"ReviewedWikiExactDictionary={NoteSvc.ReviewedWikiArabicTranslationCount}, " +
                    $"StructuredOccurrences={structured}, UniqueStructuredTranslations={structuredUnique.Count}, " +
                    $"Fallbacks={fallback}, OmittedNonEmptyNotes={omitted}, " +
                    $"ForbiddenTranslationMessageOccurrences=0, ActionableOrWarning={instructionCount}.");

                var patchTemplate = File.ReadAllText(
                    Path.Combine(repoRoot, "App", "Ctrl", "PTmp.xaml"));
                Assert(patchTemplate.Split(
                           "FlowDirection=\"{Binding TitleFlow}\"",
                           StringSplitOptions.None).Length - 1 == 2,
                    "Canonical English patch titles must use explicit LTR flow in both normal and Artemis rows.");
            });
    }

    static void PatchText_LocalOfficialCorpus(string exePath)
    {
        var install = Rpcs3InstallService.FromExe(exePath);
        var officialPath = Path.Combine(install.PatPath, "patch.yml");
        Assert(File.Exists(officialPath),
            "The selected RPCS3 installation has no local patches\\patch.yml.");

        var beforeBytes = File.ReadAllBytes(officialPath);
        var beforeWrite = File.GetLastWriteTimeUtc(officialPath);
        var beforeHash = Convert.ToHexString(SHA256.HashData(beforeBytes));
        var read = new ReadSvc().Read(officialPath, PatSrc.Official);
        Assert(read.IsOk,
            "The local Official RPCS3 patch.yml did not parse: " + read.Error);

        var officialNotes = read.Catalog.Entries
            .Select(entry => entry.Notes?.Trim())
            .Where(note => !string.IsNullOrWhiteSpace(note))
            .Cast<string>()
            .ToArray();
        var uniqueOfficialNotes = officialNotes
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var embeddedNotes = EmbeddedCorpusNotes();
        var outsideEmbedded = uniqueOfficialNotes
            .Where(note => !embeddedNotes.Contains(
                NormalizeCorpusNote(note)))
            .ToArray();

        var exact = 0;
        var structured = 0;
        var suppressed = 0;
        PatchText_WithLang(
            UiText.ArabLang,
            () =>
            {
                foreach (var note in uniqueOfficialNotes)
                {
                    var result = NoteSvc.Trans(note);
                    switch (result.Quality)
                    {
                        case NoteQual.Exact:
                            exact++;
                            break;
                        case NoteQual.Fragment:
                            structured++;
                            break;
                        case NoteQual.Fallback when string.IsNullOrWhiteSpace(result.Body):
                            suppressed++;
                            break;
                        case NoteQual.Fallback:
                            throw new InvalidOperationException(
                                "An Official runtime Note exposed an Arabic fallback: " + note);
                    }

                    Assert(ForbiddenTranslationPhrases.All(phrase =>
                               !result.Body.Contains(
                                   phrase,
                                   StringComparison.OrdinalIgnoreCase)),
                        "An Official runtime Note exposed a forbidden translation message: " + note);
                }

                Assert(exact + structured + suppressed ==
                           uniqueOfficialNotes.Length,
                    "Local Official Note accounting did not balance.");

                var suppressedEntry = read.Catalog.Entries.FirstOrDefault(entry =>
                    !string.IsNullOrWhiteSpace(entry.Notes) &&
                    string.IsNullOrWhiteSpace(NoteSvc.Trans(entry.Notes).Body));
                if (suppressedEntry is not null)
                {
                    var item = new PatItVm(PatchText_Match(suppressedEntry));
                    Assert(string.IsNullOrWhiteSpace(item.LocMsg) &&
                           string.IsNullOrWhiteSpace(item.NoteBody),
                        "An unreviewed Official Note reached the Arabic patch item surface.");
                }
            });

        var xamlText = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(
                    Path.Combine(RepoRoot(), "App"),
                    "*.xaml",
                    SearchOption.AllDirectories)
                .Select(File.ReadAllText));
        Assert(!xamlText.Contains(
                   "{Binding OrigMsg}",
                   StringComparison.Ordinal) &&
               !xamlText.Contains(
                   "{Binding OrigBody}",
                   StringComparison.Ordinal),
            "Raw unreviewed source Notes are bound into the Arabic WPF surface.");

        var afterBytes = File.ReadAllBytes(officialPath);
        Assert(beforeBytes.AsSpan().SequenceEqual(afterBytes) &&
               beforeWrite == File.GetLastWriteTimeUtc(officialPath) &&
               beforeHash == Convert.ToHexString(SHA256.HashData(afterBytes)),
            "The local Official patch.yml changed during the read-only localization audit.");

        Console.WriteLine(
            $"Local Official localization: Entries={read.Catalog.Entries.Count}, " +
            $"NonEmptyNoteOccurrences={officialNotes.Length}, UniqueNonEmptyNotes={uniqueOfficialNotes.Length}, " +
            $"UniqueOutsideEmbeddedCorpora={outsideEmbedded.Length}, Exact={exact}, Structured={structured}, " +
            $"VisibleReviewed={exact + structured}, SuppressedUnreviewed={suppressed}, " +
            $"ForbiddenTranslationMessageOccurrences=0, SHA256={beforeHash}.");
    }

    private static HashSet<string> EmbeddedCorpusNotes()
    {
        var dataRoot = Path.Combine(RepoRoot(), "App", "Data");
        var reader = new ReadSvc();
        var notes = reader.Read(
                Path.Combine(dataRoot, "art.yml"),
                PatSrc.Artemis)
            .Catalog.Entries
            .Concat(reader.Read(
                    Path.Combine(dataRoot, "can.yml"),
                    PatSrc.Can)
                .Catalog.Entries)
            .Select(entry => entry.Notes)
            .Where(note => !string.IsNullOrWhiteSpace(note))
            .Cast<string>()
            .Select(NormalizeCorpusNote)
            .ToHashSet(StringComparer.Ordinal);

        using var document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(dataRoot, "cat.json")));
        foreach (var recommendation in document.RootElement
                     .GetProperty("Recommendations")
                     .EnumerateArray())
        {
            foreach (var change in recommendation
                         .GetProperty("Changes")
                         .EnumerateArray())
            {
                if (change.TryGetProperty("Notes", out var note) &&
                    note.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(note.GetString()))
                {
                    notes.Add(NormalizeCorpusNote(note.GetString()!));
                }
            }
        }

        return notes;
    }

    private static string NormalizeCorpusNote(string value)
    {
        return string.Join(
                " ",
                value.Split(
                    [' ', '\t', '\r', '\n'],
                    StringSplitOptions.RemoveEmptyEntries))
            .Trim()
            .TrimEnd('.', '。');
    }

    private static string[] CorpusNumericTokens(string value)
    {
        return Regex.Matches(
                value,
                @"(?<![A-Za-z0-9])(?:0x[0-9A-Fa-f]+|\d+(?:[.,]\d+)*)(?!\d)",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)
            .Select(match => match.Value)
            .OrderBy(token => token, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string[] CorpusTechnicalTokens(string value)
    {
        const string pattern =
            @"\b(?:RPCS3|PPU|SPU|RSX|PRX|OVL|Vulkan|OpenGL|LLVM|ASMJIT|FPS|Hz|GPU|CPU|HUD|UI|VRAM|NVIDIA|AMD|FSR|MSAA|VSync|API|syscall)\b|" +
            @"\b(?:sys_[A-Za-z0-9_]+)\b|" +
            @"\b[A-Za-z0-9_.-]+\.(?:yml|yaml|json|exe|dat|acb|bin|sprx|prx|elf)\b";
        const string controllerPattern =
            @"\b(?:L1|L2|L3|R1|R2|R3|Select|Start|Square|Triangle|Left|Right|Up|Down|X|O)\b(?=\s*(?:\+|=))|" +
            @"(?<=\+\s)(?:L1|L2|L3|R1|R2|R3|Select|Start|Square|Triangle|Left|Right|Up|Down|X|O)\b|" +
            @"(?<=Press\s)(?:L1|L2|L3|R1|R2|R3|Select|Start|Square|Triangle|Left|Right|Up|Down|X|O)\b|" +
            @"(?<=Hold\s)(?:L1|L2|L3|R1|R2|R3|Select|Start|Square|Triangle|Left|Right|Up|Down|X|O)\b|" +
            @"(?<=pressing\s)(?:L1|L2|L3|R1|R2|R3|Select|Start|Square|Triangle|Left|Right|Up|Down|X|O)\b";
        return Regex.Matches(
                value,
                pattern,
                RegexOptions.CultureInvariant)
            .Select(match => match.Value)
            .Concat(Regex.Matches(
                    value,
                    controllerPattern,
                    RegexOptions.CultureInvariant)
                .Select(match => match.Value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(token => token, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool CorpusContainsProtectedTerm(
        string value,
        string term)
    {
        return Regex.IsMatch(
            value,
            $@"(?<![A-Za-z0-9]){Regex.Escape(term)}(?![A-Za-z0-9])",
            RegexOptions.CultureInvariant);
    }

    private static bool PreservesSemanticForce(
        string source,
        string localized)
    {
        bool HasSource(params string[] values) => values.Any(value =>
            source.Contains(value, StringComparison.OrdinalIgnoreCase));
        bool HasLocalized(params string[] values) => values.Any(value =>
            localized.Contains(value, StringComparison.Ordinal));

        var preservesRequirement = !HasSource(
                "required", "requires", "must ", "needed ", "need to ") ||
            HasLocalized("مطلوب", "يتطلب", "متطلبات", "يلزم", "يجب", "يحتاج", "تحتاج", "بحاجة", "لا تعمل", "تعمل فقط");
        var hasCondition = Regex.IsMatch(
            source,
            @"(?:^|[.!?]\s+)if\b|,\s+if\b|\bunless\b|\botherwise\b|\bonly if\b|\bwithout\b",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        var preservesCondition = !hasCondition ||
            HasLocalized("إذا", "ما لم", "وإلا", "غير ذلك", "فقط", "دون", "بدون", "إلا", "لتجنب");
        var preservesSideEffect = !HasSource(
                "causes", "breaks", "reduces", "side effect", "at the cost") ||
            HasLocalized("يسبب", "تسبب", "يجعل", "تمنع", "يزيد", "يكسر", "يخفض", "يقلل", "انخفاض", "أثر جانبي", "آثار جانبية", "مقابل", "يؤدي", "يعطل", "تعطل", "يفسد");

        return preservesRequirement &&
               preservesCondition &&
               preservesSideEffect;
    }
}
