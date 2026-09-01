using System.IO;
using System.IO.Compression;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;

using System.Text;
using System.Text.Json;
using System.Threading;
using System.Xml;
using System.Windows;
using R3Config;
using R3Config.Svc;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Vm;
using R3Config.View;
using R3Config.Application;
using R3Config.ArtImport;
using R3Config.Composition;
using R3Config.Tests;

internal static partial class Program
{
    static void Lang_Key()
    {
        var repoRoot = RepoRoot();
        var langDir = Path.Combine(repoRoot, "App", "Lang");
        var arPath = Path.Combine(langDir, "ar-SA.json");
        var enPath = Path.Combine(langDir, "en-US.json");
        Assert(File.Exists(arPath), "Expected ar-SA.json to exist.");
        Assert(File.Exists(enPath), "Expected en-US.json to exist.");

        var ar = LangRead(arPath);
        var en = LangRead(enPath);
        Assert(new HashSet<string>(ar.Keys, StringComparer.Ordinal).SetEquals(en.Keys), "Expected ar-SA and en-US language keys to match exactly.");
        var artemisLabels = new Dictionary<string, (string Arabic, string English)>
        {
            ["Tab.Cheats"] = ("شفرات أرتميس", "Artemis Patch"),
            ["Patch.Source.CheatLibrary"] = ("شفرات أرتميس", "Artemis Patch"),
            ["Patch.CheatLibrary.Title"] = ("شفرات أرتميس", "Artemis Patch"),
            ["Patch.CheatEmpty"] = ("لا توجد شفرات أرتميس متوافقة مع هذه اللعبة وإصدارها الحالي.", "No compatible Artemis patches were found for this game version."),
            ["Patch.ApplyCheatSection"] = ("تفعيل", "Enable"),
            ["Patch.ApplyCheatSectionTip"] = ("يجهز شفرات أرتميس المختارة لهذه اللعبة ثم يفعلها من خلال نظام باتشات RPCS3", "Prepares the chosen Artemis patches for this game and enables them through the RPCS3 patch system"),
            ["Patch.CheatLibrary.Missing"] = ("مكتبة شفرات أرتميس غير موجودة.", "The Artemis patch catalog is missing."),
            ["Patch.CheatLibrary.NoPrepare"] = ("لا توجد شفرات أرتميس جديدة لتجهيزها.", "No new Artemis patches need to be prepared."),
            ["Patch.CheatLibrary.Prepared"] = ("تم تجهيز {0} من شفرات أرتميس. يحتوي ملف اللعبة الآن على {1} باتش.", "Prepared {0} Artemis patch(es). The game's managed file now contains {1} patch(es)."),
            ["Patch.CheatLibrary.Notice"] = ("حدد شفرات أرتميس المتوافقة التي تريد استخدامها، ثم اختر تفعيل. مخصصة للعب الفردي فقط.", "Select the compatible Artemis patches you want to use, then choose Enable. Intended for single-player use only.")
        };
        foreach (var (key, expected) in artemisLabels)
        {
            Assert(ar[key] == expected.Arabic,
                $"Expected literal Arabic Artemis label for {key}.");
            Assert(en[key] == expected.English,
                $"Expected literal English Artemis label for {key}.");
        }

        var gameLibraryLabels = new Dictionary<string, (string Arabic, string English)>
        {
            ["Games.LibraryPathRequired"] = ("حدد مسار محاكي RPCS3 لعرض مكتبة الألعاب.", "Select the RPCS3 emulator path to display the game library."),
            ["Games.LibraryScanning"] = ("جاري فحص مكتبة ألعاب RPCS3...", "Scanning the RPCS3 game library..."),
            ["Games.LibraryScanFailed"] = ("تعذر فحص مكتبة ألعاب RPCS3. تحقق من مسار المحاكي ثم أعد المحاولة.", "Could not scan the RPCS3 game library. Check the emulator path and try again."),
            ["Games.EmptyInstalled"] = ("لم يتم العثور على ألعاب في مكتبة ألعاب RPCS3.", "No games were found in the RPCS3 game library."),
            ["Btn.BrowseTip"] = ("حدد ملف تشغيل محاكي RPCS3 (rpcs3.exe).", "Select the RPCS3 emulator executable (rpcs3.exe)."),
            ["Picker.Rpcs3Title"] = ("حدد ملف تشغيل محاكي RPCS3", "Select the RPCS3 emulator executable")
        };
        foreach (var (key, expected) in gameLibraryLabels)
        {
            Assert(ar[key] == expected.Arabic,
                $"Expected literal Arabic RPCS3 game-library label for {key}.");
            Assert(en[key] == expected.English,
                $"Expected literal English RPCS3 game-library label for {key}.");
        }

        var requiredKeys = new[]
        {
            "App.Title",
            "Lang.Toggle",
            "Lang.Current",
            "Lang.ToggleTip",
            "Games.Title",
            "Games.Empty",
            "Games.LibraryPathRequired",
            "Games.LibraryScanning",
            "Games.LibraryScanFailed",
            "Msg.GameLibraryScanFailed",
            "Games.FilterAll",
            "Games.FilterInstalled",
            "Games.FilterDetectedOnly",
            "Games.FilterDetectedOnlyTip",
            "Games.EmptyInstalled",
            "Patch.Source",
            "Patch.Source.Official",
            "Patch.Source.Canary",
            "Patch.Source.CheatLibrary",
            "Patch.Source.Local",
            "Patch.Source.Custom",
            "Patch.Source.Missing",
            "Patch.Source.Unknown",
            "Patch.Source.Tooltip",
            "Patch.CanaryNotice",
            "Patch.NoneForGame",
            "Patch.Localized.FileMissing",
            "Patch.Localized.RunAnalysisFirst",
            "Patch.Localized.NoPatchesForGame",
            "Patch.Localized.DuplicateKey",
            "Patch.Localized.ParseError",
            "Patch.Localized.UnsupportedPatchNote",
            "Shell.PatchesSection",
            "Plan.ApplySection",
            "Plan.ApplySectionTip",
            "Patch.ApplyOfficialSection",
            "Patch.ApplyOfficialSectionTip",
            "Patch.ApplyCanarySection",
            "Patch.ApplyCanarySectionTip",
            "Patch.ApplyCheatSection",
            "Patch.ApplyCheatSectionTip",
            "Patch.CanaryNote.HashMismatch",
            "PatchReadiness.Capture.SavedMismatch",
            "PatchReadiness.Capture.CatalogMismatch",
            "Patch.Direct.Disabled",
            "Patch.Direct.MergedDisabled",
            "Patch.Direct.PreparedDisabled",
            "Patch.State.Disabled",
            "Patch.Pending.Enable",
            "Patch.Pending.Disable",
            "Patch.OfficialSummaryPending",
            "Patch.OfficialSummaryNone",
            "Patch.OfficialSummaryPendingOnly",
            "Patch.CanarySummaryPending",
            "Patch.CanarySummaryNone",
            "Patch.CanarySummaryPendingOnly",
            "Patch.CanarySummaryAllIncompatible",
            "Patch.Artemis.SummaryPending",
            "Patch.Artemis.SummaryNone",
            "Patch.Artemis.SummaryPendingOnly",
            "Patch.Artemis.Options",
            "Setup.AppliedNone",
            "Setup.AppliedPendingOnly",
            "Setup.ActivationNone",
            "Setup.ActivationPendingOnly",
            "Setup.CanaryAllIncompatible",
            "Setup.Warning.HashMismatchCount",
            "Setup.Warning.HashUnknown",
            "Setup.Warning.PatchBoot",
            "Setup.Warning.PatchSafetyHigh",
            "Setup.Warning.PatchSafetyCaution",
            "Patch.Launch.BootRecommended",
            "Patch.Safety.High.Label",
            "Patch.Safety.Caution.Label",
            "Patch.Safety.CrashFreeze",
            "Patch.Safety.Progression",
            "Patch.Safety.DisableRetry",
            "Patch.Safety.GenericCaution",
            "Patch.Safety.Message",
            "Patch.GroupMeta",
            "Patch.Artemis.GroupMeta",
            "Patch.Definition.Merged",
            "Patch.Definition.NotMerged",
            "Patch.Definition.Prepared",
            "Patch.Definition.NotPrepared",
            "Patch.Definition.Existing",
            "Patch.SourceStatus.OfficialLoadedWithWarnings",
            "Patch.SourceStatus.UpdateOfficial",
            "Patch.SourceStatus.UpdateOfficialTip",
            "Msg.OfficialPatchManagerInstructions",
            "Patch.SourceStatus.PartialBadge",
            "Msg.CfgApplySuccess",
            "Msg.PatchApplyOfficialSuccess",
            "Msg.PatchApplyCanarySuccess",
            "Msg.PatchApplyCheatSuccess",
            "Patch.ImportCanaryTip",
            "Patch.SectionHint",
            "Handling.UserSelected",
            "Plan.ShowApplied",
            "Plan.SummaryCompleteFormat",
            "Plan.SummaryPendingFormat",
            "Setup.None",
            "Patch.Empty",
            "Dialog.Ok",
            "Dialog.Cancel",
            "Patch.PrepareCanary",
            "Patch.PrepareCanarySuccess",
            "Patch.PrepareCanarySelectedSuccess",
            "Patch.PrepareCanaryAlreadyReady",
            "Patch.PrepareCanaryNoSelection",
            "Patch.PrepareCanaryAlreadyInImported",
            "Patch.RemoveCanary",
            "Patch.RemoveCanaryTip",
            "Patch.RemoveCanaryConfirm",
            "Patch.RemoveCanarySuccess",
            "Patch.RemoveCanaryNothing",
            "Patch.BundledCanaryMissing",
            "Patch.CheatLibrary.Title",
            "Patch.CheatLibrary.Missing",
            "Patch.CheatLibrary.UnmanagedConflict",
            "Patch.CheatLibrary.Prepared",
            "Patch.CheatLibrary.Notice",
            "Tab.HCanary",
            "Tab.Advanced",
            "HCanary.Title",
            "HCanary.Description",
            "HCanary.Definitions",
            "HCanary.Families",
            "HCanary.SimplePatches",
            "HCanary.ComplexPatches",
            "HCanary.MultiFamilies",
            "HCanary.ScaffoldStatus",
            "HCanary.CatalogReady",
            "HCanary.CatalogError",
            "HCanary.AnalysisTitle",
            "HCanary.AnalysisUnavailable",
            "HCanary.SelectGame",
            "HCanary.NoMatches",
            "HCanary.Log.NoFile",
            "HCanary.Log.NoMatch",
            "HCanary.AnalysisSummary",
            "HCanary.Evidence.None",
            "HCanary.Evidence.Count",
            "HCanary.Field.Version",
            "HCanary.Field.Hash",
            "HCanary.ReferenceSummary",
            "HCanary.Status.ExactReference",
            "HCanary.Status.HashEvidenceMissing",
            "HCanary.Status.VersionUnsupported",
            "HCanary.Status.ConflictingReferences",
            "HCanary.Status.ComplexManualReview",
            "HCanary.Status.NotPortable",
            "HCanary.Reason.ExactReference",
            "HCanary.Reason.HashEvidenceMissing",
            "HCanary.Reason.VersionUnsupported",
            "HCanary.Reason.ConflictingReferences",
            "HCanary.Reason.ComplexManualReview",
            "HCanary.Reason.NotPortable",
            "Patch.ImportCanary",
            "Patch.ImportCanaryManual",
            "Patch.ImportCanaryManualTip",
            "Patch.ImportCanaryTitle",
            "Patch.ImportCanaryPasteLabel",
            "Patch.ImportCanaryChooseFile",
            "Patch.ImportCanaryCreateFile",
            "Patch.ImportCanarySuccess",
            "Patch.ImportCanaryNoRpcs3Path",
            "Patch.ImportCanaryInvalidYaml",
            "Patch.ImportCanaryDuplicatePpuMerged",
            "Patch.ImportCanaryDuplicatePatchName",
            "Patch.ImportCanaryUnsafePath",
            "Patch.ImportCanaryBackupCreated",
            "Patch.ImportedFileInvalid",
            "Patch.OfficialReadWarning",
            "Patch.ImportedReadWarning",
            "Patch.ImportedDuplicateKey",
            "Patch.ImportedDuplicateKeyReadable",
            "Patch.ImportedDuplicateKeySuggestion",
            "PatchReadiness.HashReady",
            "PatchReadiness.HashReadyTip",
            "PatchReadiness.NeedsColdBoot",
            "PatchReadiness.NeedsColdBootTip",
            "PatchReadiness.NeedsColdBootWarning",
            "PatchReadiness.NotDetected",
            "PatchReadiness.NotDetectedTip",
            "Patch.DuplicateKeyReadable",
            "Patch.DuplicateKeySuggestion",
            "Patch.Note.OriginalText",
            "Patch.Note.WikiNote",
            "Patch.Note.Translation",
            "Patch.Note.Type",
            "Patch.Note.Source",
            "Plan.WikiInstructions",
            "Msg.PatchCatWarning"
        };

        foreach (var key in requiredKeys)
        {
            Assert(ar.TryGetValue(key, out var arValue) && !string.IsNullOrWhiteSpace(arValue), "Required Arabic language value is empty: " + key);
            Assert(en.TryGetValue(key, out var enValue) && !string.IsNullOrWhiteSpace(enValue), "Required English language value is empty: " + key);
        }

        Assert(ar["Plan.ApplySection"] == "تطبيق الإعدادات",
            "Expected the Arabic settings action to describe an explicit apply operation.");
        Assert(ar["Patch.ApplyOfficialSection"] == "تفعيل" &&
               ar["Patch.ApplyCanarySection"] == "تفعيل" &&
               ar["Patch.ApplyCheatSection"] == "تفعيل" &&
               ar["Patch.Off"] == "تعطيل",
            "Expected every player-facing patch toolbar to use only the compact Arabic Enable and Disable labels.");
        Assert(ar["Tab.Canary"] == "باتشات Canary" &&
               ar["Patch.Source.Canary"] == "Canary",
            "Expected the Canary name to remain in navigation and source metadata without a repeated player-page source strip.");
        Assert(!ar.ContainsKey("Games.Need") &&
               !ar.ContainsKey("Games.NeedDisplay") &&
               !ar.ContainsKey("Games.NeedTip") &&
               !en.ContainsKey("Games.Need") &&
               !en.ContainsKey("Games.NeedDisplay") &&
               !en.ContainsKey("Games.NeedTip"),
            "Expected the ambiguous per-game settings counter language contract to be removed.");
        Assert(ar["Tab.Setup"] == "ملخص التعديلات" &&
               ar["Setup.PrepareAction"] == "تطبيق التعديلات" &&
               ar["Setup.AppliedCount"].StartsWith("مضبوط ", StringComparison.Ordinal),
            "Expected the final game workflow surface to use concise change-summary terminology.");
        Assert(!ar.Keys.Any(key => key.StartsWith(
                   "Patch.Text.Title.",
                   StringComparison.Ordinal)) &&
               !en.Keys.Any(key => key.StartsWith(
                   "Patch.Text.Title.",
                   StringComparison.Ordinal)),
            "Patch titles must stay canonical; localization belongs to Notes/instructions, not dynamic patch labels.");

        static bool IsArabicDiacritic(char ch) =>
            (ch >= '\u0610' && ch <= '\u061A') ||
            (ch >= '\u064B' && ch <= '\u065F') ||
            ch == '\u0670' ||
            (ch >= '\u06D6' && ch <= '\u06ED');

        foreach (var (key, value) in ar)
        {
            Assert(!value.Any(IsArabicDiacritic),
                $"Arabic UI text must not contain diacritics: {key}");
        }

        var appTextFiles = Directory.EnumerateFiles(
                Path.Combine(repoRoot, "App"),
                "*.*",
                SearchOption.AllDirectories)
            .Where(path =>
                Path.GetExtension(path).Equals(".cs", StringComparison.OrdinalIgnoreCase) ||
                Path.GetExtension(path).Equals(".xaml", StringComparison.OrdinalIgnoreCase) ||
                Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase));
        foreach (var path in appTextFiles)
        {
            Assert(!File.ReadAllText(path).Any(IsArabicDiacritic),
                "App Arabic source must not contain diacritics: " +
                Path.GetRelativePath(repoRoot, path));
        }

        var originalLanguage = UiText.CurLang;
        try
        {
            foreach (var language in new[]
                     {
                         UiText.ArabLang,
                         UiText.EnLang
                     })
            {
                UiText.SetLang(language);
                foreach (var status in Enum.GetValues<MatStat>())
                {
                    var label = UiText.PatMatch(status);
                    Assert(
                        !string.IsNullOrWhiteSpace(label) &&
                        !label.StartsWith(
                            "PatchMatch.",
                            StringComparison.Ordinal),
                        $"Expected {language}/{status} to resolve to localized text instead of a raw PatchMatch key.");
                }
            }
        }
        finally
        {
            UiText.SetLang(originalLanguage);
        }

        Console.WriteLine($"Lang keys: ar-SA/en-US parity PASS ({ar.Count}).");
    }

    static void PatchText_CanonicalFieldsRemainUnchanged()
    {
        var patch = PatchText_Entry(
            "Infinite Health",
            "Requires Write Color Buffers",
            PatSrc.Official,
            PatClass.Main);
        var match = PatchText_Match(patch);
        var before = (
            patch.Title,
            patch.Notes,
            patch.HashKind,
            patch.Hash,
            patch.Source,
            patch.Class,
            match.Status,
            match.MatSer,
            match.MatVer,
            match.TgtSer,
            match.TgtVer);

        PatchText_WithLang(
            UiText.ArabLang,
            () =>
            {
                var item = new PatItVm(match);
                Assert(item.DspTitle == patch.Title &&
                       item.LocMsg == "يتطلب تفعيل Write Color Buffers في إعدادات GPU." &&
                       item.InsText == item.LocMsg,
                    "Arabic patch presentation projection changed the canonical patch title or failed to localize its requirement. " +
                    $"DspTitle='{item.DspTitle}'; LocMsg='{item.LocMsg}'; InsText='{item.InsText}'.");
                Assert(item.Title == patch.Title &&
                       item.Notes == patch.Notes &&
                       ReferenceEquals(item.Match.Patch, patch),
                    "Patch presentation localization replaced or mutated the canonical patch object.");
            });

        var after = (
            patch.Title,
            patch.Notes,
            patch.HashKind,
            patch.Hash,
            patch.Source,
            patch.Class,
            match.Status,
            match.MatSer,
            match.MatVer,
            match.TgtSer,
            match.TgtVer);
        Assert(before == after,
            "Patch presentation localization changed canonical matching or compatibility identity fields.");
    }

    static void PatchText_EnglishPresentationRemainsCanonical()
    {
        var patch = PatchText_Entry(
            "Infinite Health",
            "Hold L2 + X to fly",
            PatSrc.Artemis,
            PatClass.Main);

        PatchText_WithLang(
            UiText.EnLang,
            () =>
            {
                var item = new PatItVm(PatchText_Match(patch));
                var note = NoteSvc.Trans(patch.Notes);
                Assert(item.DspTitle == patch.Title &&
                       item.LocMsg == patch.Notes &&
                       item.InsText == patch.Notes &&
                       note.Quality == NoteQual.Original,
                    "English patch presentation did not preserve the canonical English title and instruction.");
            });
    }

    static void PatchText_ArabicInstructionsAndRequirements()
    {
        PatchText_WithLang(
            UiText.ArabLang,
            () =>
            {
                var requirement = NoteSvc.Trans(
                    "Requires Write Color Buffers");
                var disableRequirement = NoteSvc.Trans(
                    "Requires disabling Read Color Buffers");
                var conditional = NoteSvc.Trans(
                    "Only works with Disable ZCull Occlusion Queries");
                var hold = NoteSvc.Trans(
                    "Hold L2 + X to fly");
                var press = NoteSvc.Trans(
                    "Press L3 to toggle walking in air");

                Assert(requirement.Body ==
                           "يتطلب تفعيل Write Color Buffers في إعدادات GPU." &&
                       disableRequirement.Body ==
                           "يتطلب تعطيل Read Color Buffers" &&
                       conditional.Body ==
                           "يعمل فقط عند تفعيل Disable ZCull Occlusion Queries",
                    "Known Arabic patch requirements were not structurally localized.");
                Assert(hold.Body ==
                           "اضغط باستمرار L2 + X للطيران" &&
                       press.Body ==
                           "اضغط L3 لتبديل المشي في الهواء",
                    "Known Arabic controller instructions were not structurally localized.");
                Assert(PatText.DspTitle("Infinite Health") ==
                           "Infinite Health" &&
                       PatText.DspTitle("Hold L2 + X to fly") ==
                           "Hold L2 + X to fly",
                    "Canonical patch descriptions must remain source-authored labels in every UI language.");

                var enableCode = NoteSvc.Trans(
                    "This needs to be activated before holding L2 + X to fly, toggling between Arc Lightning and Overload Burst with L2 + L1, toggling AI movement with L2 + L3, and making certain objects invincible.");
                var markObject = NoteSvc.Trans(
                    "Just hold select and then damage whatever it is that you want to mark to prevent its health from decreasing.");
                var aiStop = NoteSvc.Trans(
                    "People might not instantly stop, but once they do they will stay stopped until you toggle the code off.");

                Assert(enableCode.Body ==
                           "يجب تفعيل هذه الشفرة قبل استخدام L2 + X للطيران، أو التبديل بين Arc Lightning وOverload Burst باستخدام L2 + L1، أو تبديل حركة AI باستخدام L2 + L3، أو جعل عناصر محددة غير قابلة للضرر." &&
                       markObject.Body ==
                           "اضغط باستمرار على Select ثم ألحق ضررا بالعنصر الذي تريد تحديده لمنع انخفاض صحته." &&
                       aiStop.Body ==
                           "قد لا يتوقف الأشخاص فورا، لكن بعد توقفهم سيبقون متوقفين حتى تعطل الشفرة.",
                    "Known Artemis notes were not localized as explanatory text separate from their canonical patch labels. " +
                    $"EnableCode='{enableCode.Body}'; MarkObject='{markObject.Body}'; AiStop='{aiStop.Body}'.");

                var exactCause = NoteSvc.Trans(
                    "Original resolution is 1024x600. Causes rendering bugs");
                var normalValue = NoteSvc.Trans(
                    "3F800000=Normal;40000000=2x;40400000=3x;40800000=4x;40B00000=5x");
                var authorInstruction = NoteSvc.Trans(
                    "WIP. Contact me for patch revisions");
                var sideEffectReport = NoteSvc.Trans(
                    "Improves sound cackling by disabling some functions related to the audio. Lightning sounds might be distorted. Please, report any other side effects");

                Assert(exactCause.Body ==
                           "الدقة الأصلية هي 1024x600. يسبب هذا الخيار مشكلات في العرض." &&
                       normalValue.Body.StartsWith(
                           "القيم: 3F800000=عادي؛",
                           StringComparison.Ordinal) &&
                       !normalValue.Body.Contains(
                           "3F800000=افتراضي",
                           StringComparison.Ordinal) &&
                       authorInstruction.Body ==
                           "قيد التطوير. تواصل مع مؤلف الباتش للحصول على النسخ المنقحة." &&
                       sideEffectReport.Body.EndsWith(
                           "يرجى الإبلاغ عن أي آثار جانبية أخرى.",
                           StringComparison.Ordinal),
                    "Arabic note localization lost source certainty, Normal/default semantics, author instructions, or side-effect reporting.");
            });
    }

    static void PatchText_TechnicalAndControllerTokensRemainCanonical()
    {
        PatchText_WithLang(
            UiText.ArabLang,
            () =>
            {
                var requirement = NoteSvc.Trans(
                    "Requires Write Color Buffers");
                var controller = NoteSvc.Trans(
                    "Hold L2 + X to fly");

                Assert(PatText.DspTitle("Write Color Buffers") ==
                           "Write Color Buffers" &&
                       PatText.DspTitle("Read Color Buffers") ==
                           "Read Color Buffers" &&
                       PatText.DspTitle("PPU Decoder") ==
                           "PPU Decoder" &&
                       PatText.DspTitle("SPU Decoder") ==
                           "SPU Decoder" &&
                       PatText.DspTitle("(Experimental) Disable visual effects") ==
                           "(Experimental) Disable visual effects" &&
                       PatText.DspTitle("(b) Hold L2 + X To Fly") ==
                           "(b) Hold L2 + X To Fly" &&
                       requirement.Body.Contains(
                           "Write Color Buffers",
                           StringComparison.Ordinal) &&
                       !requirement.Body.Contains(
                           "كتابة مخازن",
                           StringComparison.Ordinal),
                    "A canonical patch label or official RPCS3 setting name was translated or no longer discoverable.");
                Assert(controller.Body.Contains(
                           "L2",
                           StringComparison.Ordinal) &&
                       controller.Body.Contains('X') &&
                       !controller.Body.Contains(
                           "Hold",
                           StringComparison.OrdinalIgnoreCase),
                    "Controller tokens were changed or their English instruction scaffold leaked.");
            });
    }

    static void PatchText_UnknownAndDiagnosticTextDoesNotLeak()
    {
        PatchText_WithLang(
            UiText.ArabLang,
            () =>
            {
                const string requiredRaw =
                    "Requires a custom setup step before use";
                const string optionalRaw =
                    "Optional performance flavor text";
                const string instructionRaw =
                    "Set the flux lattice to wobble mode";
                var required = PatText.PatMsg(requiredRaw);
                var optional = PatText.PatMsg(optionalRaw);
                var instruction = PatText.PatMsg(instructionRaw);
                var diagnostic = PatText.PatMsg(
                    "HASH_UNKNOWN: HASHMISMATCH performance note");
                var requiredItem = new PatItVm(PatchText_Match(
                    PatchText_Entry(
                        "Required Fixture",
                        requiredRaw,
                        PatSrc.Official,
                        PatClass.Main)));

                Assert(NoteSvc.Classify(requiredRaw) ==
                           NoteKind.Requirement &&
                       NoteSvc.IsInstruction(requiredRaw) &&
                       required.Kind == NoteKind.Requirement &&
                       required.IsInstruction &&
                       required.Body ==
                           "توجد تعليمات مطلوبة لهذا الخيار لكن ترجمتها العربية غير متوفرة حاليا" &&
                       required.HasOrig &&
                       required.OrigBody == requiredRaw,
                    "An unknown required instruction lost its semantic classification, Arabic fallback, or secondary original reference.");
                Assert(requiredItem.HasIns &&
                       !string.IsNullOrWhiteSpace(requiredItem.InsText) &&
                       requiredItem.InsText == required.Body,
                    "An unknown required instruction disappeared from the primary Arabic instruction surface.");
                Assert(instruction.Kind == NoteKind.Instruction &&
                       instruction.IsInstruction &&
                       instruction.Body == required.Body &&
                       instruction.HasOrig &&
                       instruction.OrigBody == instructionRaw,
                    "An unknown imperative instruction lost its classification, Arabic fallback, or secondary original reference.");
                Assert(optional.Kind == NoteKind.Description &&
                       !optional.IsInstruction &&
                       string.IsNullOrEmpty(optional.Body) &&
                       optional.HasOrig &&
                       optional.OrigBody == optionalRaw,
                    "An optional unknown description was exposed as primary Arabic text or lost its secondary original reference.");
                Assert(string.IsNullOrEmpty(diagnostic.Body) &&
                       !diagnostic.Body.Contains(
                           "HASH_UNKNOWN",
                           StringComparison.OrdinalIgnoreCase) &&
                       !diagnostic.Body.Contains(
                           "HASHMISMATCH",
                           StringComparison.OrdinalIgnoreCase),
                    "Internal hash diagnostic tokens leaked into the primary Arabic patch description.");
            });
    }

    static void PatchText_UnknownTitlesRemainCanonical()
    {
        const string rawTitle =
            "Uncatalogued Gameplay Modifier";
        var patch = PatchText_Entry(
            rawTitle,
            "Optional undocumented flavor text",
            PatSrc.Artemis,
            PatClass.Main);
        var titleBytes = Encoding.UTF8.GetBytes(patch.Title);

        PatchText_WithLang(
            UiText.ArabLang,
            () =>
            {
                var item = new PatItVm(PatchText_Match(patch));
                Assert(item.DspTitle == rawTitle &&
                       PatText.DspTitle(rawTitle) == rawTitle,
                    "An unknown canonical gameplay title was replaced by a generic localization fallback.");
            });

        Assert(patch.Title == rawTitle &&
               Encoding.UTF8.GetBytes(patch.Title)
                   .SequenceEqual(titleBytes),
            "Unknown canonical Patch.Title bytes changed during presentation localization.");
    }

    static void PatchText_SourceKindsSharePresentationBoundary()
    {
        var sources = new[]
        {
            (PatSrc.Official, PatClass.Main, PatSrcKd.Official),
            (PatSrc.Can, PatClass.Canary, PatSrcKd.Canary),
            (PatSrc.Artemis, PatClass.Main, PatSrcKd.Artemis)
        };

        PatchText_WithLang(
            UiText.ArabLang,
            () =>
            {
                foreach (var (source, patchClass, expectedKind) in sources)
                {
                    var patch = PatchText_Entry(
                        "Hold L2 + X to fly",
                        "Requires Write Color Buffers",
                        source,
                        patchClass);
                    var rawTitle = patch.Title;
                    var rawNotes = patch.Notes;
                    var item = new PatItVm(PatchText_Match(patch));

                    Assert(item.PatSrcKd == expectedKind &&
                           item.DspTitle == rawTitle &&
                           item.LocMsg ==
                               "يتطلب تفعيل Write Color Buffers في إعدادات GPU.",
                        $"{expectedKind} did not preserve its canonical patch label while localizing explanatory Notes.");
                    Assert(patch.Title == rawTitle &&
                           patch.Notes == rawNotes &&
                           patch.Source == source &&
                           patch.Class == patchClass,
                        $"{expectedKind} raw patch data changed during presentation localization.");
                }

                var patchTemplate = File.ReadAllText(
                    Path.Combine(RepoRoot(), "App", "Ctrl", "PTmp.xaml"));
                Assert(patchTemplate.Contains(
                           "Text=\"{Binding NoteBody}\"",
                           StringComparison.Ordinal) &&
                       patchTemplate.Contains(
                           "Visibility=\"{Binding HasDesc,",
                           StringComparison.Ordinal),
                    "Artemis presentation must expose localized Notes separately from the canonical patch label.");
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

        Assert(officialChangeCount >= 7000,
            "Expected the complete bundled RPCS3 Wiki recommendation corpus before localization audit.");

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
                var unreviewedBundledNotes = new List<string>();

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

                    if (!string.IsNullOrWhiteSpace(result.Body))
                    {
                        Assert(NoteSvc.ContainsArabic(result.Body) || NoteSvc.ContainsArabic(raw),
                            $"{source} localized patch note has no Arabic presentation text: {title}");
                    }

                    if (source is "Artemis" or "Canary")
                    {
                        Assert(!string.IsNullOrWhiteSpace(result.Body),
                            $"{source} bundled Note has no Arabic presentation coverage: {title} :: {raw}");
                        if (result.Quality == NoteQual.Fallback)
                        {
                            unreviewedBundledNotes.Add(
                                $"{source}: {title} :: {raw}");
                        }
                    }

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
                            break;
                        case NoteQual.Fragment:
                            structured++;
                            break;
                        case NoteQual.Fallback when string.IsNullOrWhiteSpace(result.Body):
                            omitted++;
                            break;
                        case NoteQual.Fallback:
                            fallback++;
                            break;
                    }
                }

                Assert(unreviewedBundledNotes.Count == 0,
                    "Bundled notes fell back instead of using reviewed corpus localization:" +
                    Environment.NewLine +
                    string.Join(Environment.NewLine, unreviewedBundledNotes.Distinct(StringComparer.Ordinal)));

                Console.WriteLine(
                    $"Patch localization corpus: Artemis={artRead.Catalog.Entries.Count}, " +
                    $"Canary={canRead.Catalog.Entries.Count}, WikiChanges={officialChangeCount}, " +
                    $"Notes={notes.Count}, Exact={exact}, Structured={structured}, " +
                    $"Fallback={fallback}, OmittedOptional={omitted}, ActionableOrWarning={instructionCount}.");

                var patchTemplate = File.ReadAllText(
                    Path.Combine(repoRoot, "App", "Ctrl", "PTmp.xaml"));
                Assert(patchTemplate.Split(
                           "FlowDirection=\"{Binding TitleFlow}\"",
                           StringSplitOptions.None).Length - 1 == 2,
                    "Canonical English patch titles must use explicit LTR flow in both normal and Artemis rows.");
            });
    }

    static Rpcs3PatchEntry PatchText_Entry(
        string title,
        string notes,
        PatSrc source,
        PatClass patchClass)
    {
        return new Rpcs3PatchEntry(
            Title: title,
            HashKind: HashKind.Ppu,
            Hash: "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            GameTgt:
            [
                new R3PatTgt(
                    "Patch Localization Fixture",
                    "BLUS00001",
                    ["01.00"])
            ],
            PatType: "Cheat",
            Risk: R3Config.Mdl.PatRisk.Cheat,
            Notes: notes,
            Author: "R3Test",
            PatVer: "1.0",
            Group: "Localization",
            Source: source,
            SrcPath: "presentation-fixture.yml",
            Class: patchClass);
    }

    static PatMatch PatchText_Match(Rpcs3PatchEntry patch)
    {
        return new PatMatch(
            patch,
            MatStat.Match,
            MatSer: "BLUS00001",
            MatVer: "01.00",
            HasMeta: true,
            Reason: "Patch presentation fixture",
            TgtName: "Patch Localization Fixture",
            TgtSer: "BLUS00001",
            TgtVer: "01.00",
            DetHash: [patch.Hash],
            HashSrc: "R3Test",
            HashEvi: HashEvi.Meta);
    }

    static void PatchText_WithLang(
        string language,
        Action action)
    {
        var originalLanguage = UiText.CurLang;
        try
        {
            UiText.SetLang(language);
            action();
        }
        finally
        {
            UiText.SetLang(originalLanguage);
        }
    }

}
