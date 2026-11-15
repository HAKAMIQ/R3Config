using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows.Threading;
using R3Config.Mdl;
using R3Config.Vm;

namespace R3Config.Tests;

internal static class NclUiT
{
    private static readonly string[] NclStateBindings =
    [
        "CheatHint",
        "RunKeyHint",
        "PreviewStatus"
    ];

    private static readonly string[] NclDialogForbiddenSymbols =
    [
        "PatchFileTransactionService",
        "PatchApplyService",
        "File.WriteAllText",
        "patch_config"
    ];

    private static readonly string[] MojibakeMarkers =
    [
        "ط§",
        "طھ",
        "ط±",
        "ط¨",
        "ط©",
        "ط¹",
        "ط®",
        "ط¬",
        "ط¥",
        "ط£",
        "ظٹ",
        "ظ„",
        "ظ…",
        "ظˆ",
        "ظپ",
        "ظ‚",
        "ظ†",
        "طŒ",
        "طں",
        "Ø",
        "Ù"
    ];

    public static void Run()
    {
        TechnicalTextT.Run();
        BusyBoundaryPreservesSelectionAndCloseOwnership();
        NclRunKeySelectionPolicy();
        var root = FindRepoRoot();

        var useCases = Read(root, "App", "Application", "AppUseCases.cs");
        var composition = Read(root, "App", "Composition", "AppComposition.cs");
        var flow = Read(root, "App", "Flow.cs");
        var managed = Read(root, "App", "Application", "NclManagedPatchUseCase.cs");
        var corpus = Read(root, "Core", "Svc", "Ncl", "NclCorpusReader.cs");
        var main = Read(root, "App", "Vm", "Main.cs");
        var mainNcl = Read(root, "App", "Vm", "Main.Ncl.cs");
        var mainUi = Read(root, "App", "Vm", "Main.Ui.cs");
        var dlg = Read(root, "App", "Svc", "Dlg.cs");
        var nclVm = Read(root, "App", "Vm", "NclImportVm.cs");
        var uiGate = Read(root, "Tst", "UiGate.cs");
        var view = Read(root, "App", "Ctrl", "CheatPatchView.xaml");
        var dialog = Read(root, "App", "View", "NclImportDlg.xaml");
        var patTmp = Read(root, "App", "Ctrl", "PatchTemplates.xaml");
        var ar = Read(root, "App", "Lang", "ar-SA.json");
        var en = Read(root, "App", "Lang", "en-US.json");

        NclOptionSelectionContract(managed, corpus, nclVm, dialog, ar, en);

        Assert(
            useCases.Contains(
                "NclManagedPatchUseCase? NclManagedPatch = null",
                StringComparison.Ordinal),
            "AppUseCases must expose the NCL managed use case without breaking existing construction callers.");

        Assert(
            composition.Contains(
                "var patchMutation = new PatchMutationUseCase(",
                StringComparison.Ordinal) &&
            composition.Contains(
                "new NclManagedPatchUseCase(",
                StringComparison.Ordinal) &&
            composition.Contains(
                "bundledCheats,",
                StringComparison.Ordinal) &&
            composition.Contains(
                "patchMutation,",
                StringComparison.Ordinal) &&
            composition.Contains(
                "NclManagedPatchUseCase.BundledCorpusName",
                StringComparison.Ordinal),
            "NCL composition must share the existing patch mutation boundary and inject the pinned bundled corpus through the same use case.");

        Assert(
            flow.Contains(
                "DiscoverBundledNcl(",
                StringComparison.Ordinal) &&
            flow.Contains(
                "PrepareBundledNclManagedPatch(",
                StringComparison.Ordinal) &&
            flow.Contains(
                "InspectNcl(",
                StringComparison.Ordinal) &&
            flow.Contains(
                "PrepareNclManagedPatch(",
                StringComparison.Ordinal) &&
            flow.Contains(
                "ApplyNclManagedPatch(",
                StringComparison.Ordinal),
            "AppFlow must remain the single thin NCL UI delegation facade for bundled discovery, bundled preview, external import, and apply.");

        Assert(
            managed.Contains(
                "SourceSerialMismatch",
                StringComparison.Ordinal) &&
            managed.Contains(
                "SourceVersionMismatch",
                StringComparison.Ordinal) &&
            managed.Contains(
                "NclStaticLowerer.Lower(cheat)",
                StringComparison.Ordinal) &&
            managed.Contains(
                "NclCorpusReader.LookupZip(",
                StringComparison.Ordinal) &&
            managed.Contains(
                "NclCorpusReader.ReadZipCandidate(",
                StringComparison.Ordinal) &&
            managed.Contains(
                "ApplyPreparedManaged(",
                StringComparison.Ordinal),
            "NCL must fail closed on identity/semantic changes while bundled and external sources converge on the existing managed mutation transaction.");

        Assert(
            main.Contains(
                "NclImportCmd = new RelayCmd(",
                StringComparison.Ordinal) &&
            main.Contains(
                "public RelayCmd NclImportCmd { get; }",
                StringComparison.Ordinal) &&
            mainUi.Contains(
                "NclImportCmd.RaiseCan();",
                StringComparison.Ordinal),
            "MainVm must expose and refresh the NCL import command.");

        // The ViewModel hands the live enabled-patch snapshot to NclImportVm and
        // asks UiDlg to host the dialog; UiDlg is the only place that constructs
        // NclImportDlg and it owns the dialog with the active window. Both halves
        // are pinned, so neither the snapshot nor the hosting can be bypassed.
        Assert(
            mainNcl.Contains(
                "Preview.Enabled()",
                StringComparison.Ordinal) &&
            mainNcl.Contains(
                "UiDlg.ShowNclImport(viewModel)",
                StringComparison.Ordinal) &&
            dlg.Contains(
                "ShowNclImport(NclImportVm viewModel)",
                StringComparison.Ordinal) &&
            dlg.Contains(
                "new NclImportDlg(Owner(), viewModel)",
                StringComparison.Ordinal),
            "NCL dialog must receive the live enabled-patch snapshot and use the existing cheat workflow, hosted by UiDlg so the ViewModel never constructs the window.");

        Assert(
            view.Contains(
                "x:Name=\"NclImportMenuItem\"",
                StringComparison.Ordinal) &&
            view.Contains(
                "x:Name=\"NclImportEmptyButton\"",
                StringComparison.Ordinal) &&
            view.Contains(
                "Command=\"{Binding NclImportCmd}\"",
                StringComparison.Ordinal) &&
            view.Contains(
                "Visibility=\"{Binding Preview.HasCht,",
                StringComparison.Ordinal) &&
            view.Contains(
                "Visibility=\"{Binding HasGame,",
                StringComparison.Ordinal) &&
            view.Contains(
                "ToolTip=\"{lang:TextExt Key=Patch.Ncl.ImportTip}\"",
                StringComparison.Ordinal) &&
            view.Split(
                "ToolTip=\"{lang:TextExt Key=Patch.Ncl.ImportTip}\"",
                System.StringSplitOptions.None).Length == 2,
            "NCL import must preserve the existing conditional cheat mutation row, remain in Code options when cheats exist, remain reachable from the empty cheat state for a selected game, and keep a single NCL import ToolTip owner.");

        Assert(
            nclVm.Contains(
                "public string FilePath",
                StringComparison.Ordinal) &&
            nclVm.Contains(
                "private set => Set(ref _filePath, value);",
                StringComparison.Ordinal) &&
            dialog.Contains(
                "Text=\"{Binding FilePath, Mode=OneWay}\"",
                StringComparison.Ordinal) &&
            !dialog.Contains(
                "Text=\"{Binding FilePath, Mode=TwoWay",
                StringComparison.Ordinal),
            "NCL FilePath must remain read-only in the VM and OneWay in WPF; do not add a setter to satisfy binding defaults.");

        Assert(
            nclVm.Contains(
                "RunOperationAsync(LoadBundled)",
                StringComparison.Ordinal) &&
            nclVm.Contains(
                "DiscoverBundledNcl(",
                StringComparison.Ordinal) &&
            nclVm.Contains(
                "PrepareBundledNclManagedPatch(",
                StringComparison.Ordinal) &&
            dialog.Contains(
                "ItemsSource=\"{Binding Sources}\"",
                StringComparison.Ordinal) &&
            dialog.Contains(
                "SelectedItem=\"{Binding SelectedSource, Mode=TwoWay}\"",
                StringComparison.Ordinal) &&
            dialog.Contains(
                "DataTrigger Binding=\"{Binding IsExternalSource, Mode=OneWay}\"",
                StringComparison.Ordinal) &&
            dialog.Contains(
                "SelectedDialect",
                StringComparison.Ordinal) &&
            dialog.Contains(
                "SelectedCheat",
                StringComparison.Ordinal) &&
            dialog.Contains(
                "SelectedRunKey",
                StringComparison.Ordinal) &&
            dialog.Contains(
                "PreviewCmd",
                StringComparison.Ordinal) &&
            dialog.Contains(
                "ApplyCmd",
                StringComparison.Ordinal),
            "NCL dialog must be bundled-database-first, keep external import explicit/advanced, require external dialect selection, and preserve cheat/PPU/preview/apply stages.");

        NclSourceOrderingContract(nclVm, dialog);
        NclEvidenceAndDirectionContract(managed, nclVm, dialog, ar, en);

        Assert(
            dialog.Contains(
                "Background=\"Transparent\"",
                StringComparison.Ordinal) &&
            dialog.Contains(
                "Foreground=\"{DynamicResource Text.Primary}\"",
                StringComparison.Ordinal) &&
            dialog.Contains(
                "FontFamily=\"{DynamicResource Type.Font.Body}\"",
                StringComparison.Ordinal) &&
            dialog.Contains(
                "Style=\"{StaticResource FluentSurface.Dialog}\"",
                StringComparison.Ordinal) &&
            dialog.Contains(
                "Style=\"{StaticResource R3DialogTopBar}\"",
                StringComparison.Ordinal) &&
            dialog.Contains(
                "Style=\"{StaticResource R3DialogActionBar}\"",
                StringComparison.Ordinal) &&
            dialog.Contains(
                "Style=\"{StaticResource PathTextBox}\"",
                StringComparison.Ordinal) &&
            dialog.Contains(
                "BasedOn=\"{StaticResource FluentComboBox}\"",
                StringComparison.Ordinal) &&
            dialog.Contains(
                "BasedOn=\"{StaticResource FluentButton.Primary}\"",
                StringComparison.Ordinal) &&
            dialog.Contains(
                "BasedOn=\"{StaticResource FluentButton.Secondary}\"",
                StringComparison.Ordinal) &&
            dialog.Contains(
                "CornerRadius=\"{StaticResource Radius.",
                StringComparison.Ordinal) &&
            !dialog.Contains(
                "Background=\"White\"",
                StringComparison.OrdinalIgnoreCase) &&
            !dialog.Contains(
                "Background=\"#",
                StringComparison.OrdinalIgnoreCase),
            "NCL dialog must stay inside the shared R3Config Fluent/theme design language without hard-coded white or hex visual islands.");

        Assert(
            uiGate.Contains(
                "new NclImportDlg(",
                StringComparison.Ordinal) &&
            uiGate.Contains(
                "new Size(660, 560)",
                StringComparison.Ordinal) &&
            uiGate.Contains(
                "new Size(760, 650)",
                StringComparison.Ordinal) &&
            uiGate.Contains(
                "NclImportDlg.png",
                StringComparison.Ordinal),
            "NCL import must remain in the runtime UI matrix for min/default sizes, DPI, language, theme, and screenshot validation.");

        Assert(
            dialog.Contains(
                "FlowDirection=\"{DynamicResource App.FlowDirection}\"",
                StringComparison.Ordinal) &&
            dialog.Contains(
                "Language=\"{DynamicResource App.XmlLanguage}\"",
                StringComparison.Ordinal) &&
            dialog.Contains(
                "FlowDirection=\"{DynamicResource App.TextFlowDirection}\"",
                StringComparison.Ordinal) &&
            dialog.Contains(
                "Value=\"{DynamicResource TechnicalText.FlowDirection}\"",
                StringComparison.Ordinal),
            "NCL dialog must preserve localized RTL/LTR flow while keeping technical path/hash content in technical LTR flow.");

        var arNcl = ReadNclLocalization(ar);
        var enNcl = ReadNclLocalization(en);

        Assert(
            arNcl.Keys.OrderBy(key => key, StringComparer.Ordinal)
                .SequenceEqual(
                    enNcl.Keys.OrderBy(key => key, StringComparer.Ordinal),
                    StringComparer.Ordinal),
            "Patch.Ncl.* localization keys must remain Arabic/English parity.");

        foreach (var pair in arNcl)
        {
            Assert(
                !ContainsMojibake(pair.Value),
                $"Arabic NCL localization contains mojibake: {pair.Key}");
            Assert(
                !pair.Value.Any(c => c is >= '\u0610' and <= '\u061A' or
                    >= '\u064B' and <= '\u065F' or '\u0670' or >= '\u06D6' and <= '\u06ED'),
                $"Arabic NCL localization contains forbidden diacritics: {pair.Key}");
        }

        Assert(
            arNcl["Patch.Ncl.Import"] == "شفرات NCL" &&
            arNcl["Patch.Ncl.Title"] == "شفرات NCL" &&
            arNcl["Patch.Ncl.Source"] == "المصدر" &&
            arNcl["Patch.Ncl.Corpus.Source"] == "قاعدة NCL المدمجة" &&
            arNcl["Patch.Ncl.External.Source"] == "ملف NCL خارجي" &&
            arNcl["Patch.Ncl.ChooseFile"] == "اختيار ملف NCL" &&
            arNcl["Patch.Ncl.Apply"] == "تطبيق الشفرة المحولة" &&
            arNcl["Patch.Ncl.Close"] == "إغلاق",
            "Critical Arabic bundled/external NCL labels must remain valid UTF-8 Arabic text.");

        // P1 regression: the three NCL state strings must be visible.
        // CheatHint, RunKeyHint and PreviewStatus were produced by the view
        // model but bound to nothing, so a multi-cheat result rendered as an
        // unexplained empty ComboBox.
        foreach (var binding in NclStateBindings)
        {
            Assert(
                nclVm.Contains(
                    $"public string {binding}",
                    StringComparison.Ordinal),
                $"NclImportVm must keep producing the {binding} state string.");

            Assert(
                dialog.Contains(
                    $"Text=\"{{Binding {binding}}}\"",
                    StringComparison.Ordinal),
                $"NclImportDlg must bind {binding} so the user can read NCL state.");
        }

        Assert(
            dialog.Contains(
                "<Style x:Key=\"NclHintText\"",
                StringComparison.Ordinal) &&
            dialog.Contains(
                "Style=\"{StaticResource NclHintText}\"",
                StringComparison.Ordinal),
            "NCL hint text blocks must use the shared NclHintText style.");

        Assert(
            dialog.Contains(
                "RelativeSource={RelativeSource Self}, Path=Text",
                StringComparison.Ordinal) &&
            dialog.Contains(
                "Value=\"Collapsed\"",
                StringComparison.Ordinal),
            "An empty NCL hint must collapse instead of leaving blank layout.");

        // NclRunKeySelectionPolicy verifies multi-cheat selection and hints
        // through the live view model for both bundled and external sources.
        Assert(
            arNcl["Patch.Ncl.CheatMultiple"].Contains(
                "اختر",
                StringComparison.Ordinal),
            "The multi-cheat Arabic hint must instruct the user to choose a cheat.");

        // P1 is presentation only: the dialog must open no writable path.
        foreach (var forbidden in NclDialogForbiddenSymbols)
        {
            Assert(
                !dialog.Contains(forbidden, StringComparison.Ordinal),
                $"NclImportDlg.xaml must not reference the writable path '{forbidden}'.");
        }

        // Conversion stays in its dialog; source candidates never replace
        // the normal Artemis enable/disable rows.
        Assert(
            !view.Contains("NclSrcSection", StringComparison.Ordinal) &&
            !view.Contains("NclSrcItems", StringComparison.Ordinal) &&
            !patTmp.Contains("NclSrcItem", StringComparison.Ordinal) &&
            !patTmp.Contains("{Binding ConvertCmd}", StringComparison.Ordinal),
            "The Artemis surface must not contain standalone NCL conversion rows.");

        Assert(
            typeof(MainVm).GetProperty("NclSrcItems") is null &&
            !mainUi.Contains("RefreshNclSources", StringComparison.Ordinal) &&
            !mainNcl.Contains("DiscoverBundledNcl", StringComparison.Ordinal) &&
            !mainNcl.Contains("ChtItems", StringComparison.Ordinal) &&
            !mainNcl.Contains("_matches", StringComparison.Ordinal),
            "The NCL dialog entry point must not discover source rows or replace Artemis patch lists.");

        Console.WriteLine(
            "NCL UI integration source contract PASS.");
    }

    // Artemis [Z] option selection: the source contract that keeps explicit
    // choice, Preview invalidation and canonical raw values in place.
    // Evidence-based PPU eligibility, honest technical direction, and a
    // user-facing rejection reason that does not replace the canonical internal
    // state but sits in front of it.
    private static void NclEvidenceAndDirectionContract(
        string managed,
        string nclVm,
        string dialog,
        string ar,
        string en)
    {
        Assert(
            managed.Contains(
                "NclPpuConversionService.DescribeEligibleRunKeys(",
                StringComparison.Ordinal) &&
            managed.Contains(
                "public NclRunKeyEligibilityResult DescribeBundledRunKeys(",
                StringComparison.Ordinal) &&
            managed.Contains(
                "public NclRunKeyEligibilityResult DescribeRunKeys(",
                StringComparison.Ordinal),
            "PPU eligibility must go through the single existing conversion/evidence contract rather than a second PPU matcher.");

        Assert(
            nclVm.Contains(
                "public ObservableCollection<string> EligibleRunKeys",
                StringComparison.Ordinal) &&
            nclVm.Contains(
                "private async Task RecomputeRunKeys()",
                StringComparison.Ordinal) &&
            nclVm.Contains(
                "_selectedRunKey = EligibleRunKeys[0];",
                StringComparison.Ordinal) &&
            dialog.Contains(
                "ItemsSource=\"{Binding EligibleRunKeys}\"",
                StringComparison.Ordinal) &&
            !dialog.Contains(
                "ItemsSource=\"{Binding RunKeys}\"",
                StringComparison.Ordinal),
            "Only PPU executables proven by address-domain evidence may be presented, and exactly one of them may auto-select.");

        Assert(
            nclVm.Contains(
                "EligibleRunKeys.Contains(",
                StringComparison.Ordinal) &&
            nclVm.Contains(
                "ClearRunKeyEligibility();",
                StringComparison.Ordinal) &&
            nclVm.Contains(
                "OptionSelectors.All(item => item.HasSelection);",
                StringComparison.Ordinal),
            "A PPU selection must be dropped whenever what proved it changes, and the PPU stage must wait for complete option state.");

        Assert(
            !managed.Contains(
                "$\"{Index + 1}. {Name}\"",
                StringComparison.Ordinal) &&
            managed.Contains(
                "int DuplicateOrdinal = 0",
                StringComparison.Ordinal) &&
            nclVm.Contains(
                "private void AddCheats(",
                StringComparison.Ordinal),
            "The internal cheat index must not be shown beside a cheat name; only genuinely duplicated names get a user-facing occurrence number.");

        Assert(
            dialog.Contains(
                "Converter={StaticResource ContentTextFlowConverter}}\"",
                StringComparison.Ordinal) &&
            dialog.Contains(
                "Text=\"{Binding Label}\"",
                StringComparison.Ordinal) &&
            dialog.Contains(
                "Text=\"{Binding Name}\"",
                StringComparison.Ordinal) &&
            dialog.Contains(
                "IsSynchronizedWithCurrentItem=\"False\"",
                StringComparison.Ordinal) &&
            dialog.Contains(
                "Style=\"{StaticResource TechnicalTextBox}\"",
                StringComparison.Ordinal),
            "Cheat names must take their direction from their own content, technical option and dialect values must render LeftToRight, and no selector may take its selection from collection currency.");

        Assert(
            nclVm.Contains(
                "private static string PreviewReason(",
                StringComparison.Ordinal) &&
            nclVm.Contains(
                "PreviewStatus = PreviewReason(preview);",
                StringComparison.Ordinal) &&
            nclVm.Contains(
                "$\"Preview: {preview.Code}\"",
                StringComparison.Ordinal) &&
            nclVm.Contains(
                "lines.Add($\"Conversion: {conversion.Code}\");",
                StringComparison.Ordinal),
            "The canonical internal codes must stay in the secondary Details surface while the primary status carries a localized reason.");

        string[] reasonKeys =
        [
            "\"Patch.Ncl.RunKeyNoneEligible\"",
            "\"Patch.Ncl.RunKeyUndetermined\"",
            "\"Patch.Ncl.Reason.OptionSelectionRequired\"",
            "\"Patch.Ncl.Reason.PpuSelectionRequired\"",
            "\"Patch.Ncl.Reason.PpuEvidenceMissing\"",
            "\"Patch.Ncl.Reason.PpuAddressMismatch\"",
            "\"Patch.Ncl.Reason.BindingRejected\"",
            "\"Patch.Ncl.Reason.NotStatic\"",
            "\"Patch.Ncl.Reason.SourceRejected\"",
            "\"Patch.Ncl.Reason.VerificationRejected\"",
            "\"Patch.Ncl.Reason.EmissionRejected\"",
            "\"Patch.Ncl.Reason.SelectionInvalid\"",
            "\"Patch.Ncl.Reason.PatchRejected\"",
            "\"Patch.Ncl.Reason.PlanRejected\"",
            "\"Patch.Ncl.Reason.PreparationRejected\"",
            "\"Patch.Ncl.Reason.Unknown\""
        ];

        foreach (var key in reasonKeys)
        {
            Assert(
                ar.Contains(key, StringComparison.Ordinal) &&
                en.Contains(key, StringComparison.Ordinal) &&
                nclVm.Contains(
                    key.Trim('"'),
                    StringComparison.Ordinal),
                $"User-facing NCL reason {key} must exist in both language packs and actually be used.");
        }
    }

    // Workflow ordering contract. The dialog has to read top to bottom as
    // identity -> source -> source-specific controls -> cheat -> options ->
    // PPU run key -> preview -> apply, and each stage may only appear once the
    // stage above it has produced real state.
    private static void NclSourceOrderingContract(
        string nclVm,
        string dialog)
    {
        var order = new[]
        {
            "Text=\"{Binding GameIdentity}\"",
            "ItemsSource=\"{Binding Sources}\"",
            "Style=\"{StaticResource NclBundledSourceStage}\"",
            "Style=\"{StaticResource NclExternalSourceStage}\"",
            "ItemsSource=\"{Binding Cheats}\"",
            "ItemsSource=\"{Binding OptionSelectors}\"",
            "Style=\"{StaticResource NclPpuRunKeyStage}\"",
            "Command=\"{Binding PreviewCmd}\"",
            "Command=\"{Binding ApplyCmd}\""
        };

        var previous = -1;
        foreach (var marker in order)
        {
            var index = dialog.IndexOf(
                marker,
                StringComparison.Ordinal);

            Assert(
                index > previous,
                $"The NCL dialog must present {marker} after the stage above it; the PPU run key must never precede source or cheat selection.");

            previous = index;
        }

        Assert(
            dialog.Contains(
                "Content=\"{lang:TextExt Key=Patch.Ncl.ChooseFile}\"",
                StringComparison.Ordinal) &&
            dialog.Contains(
                "Command=\"{Binding PickCmd}\"",
                StringComparison.Ordinal) &&
            !dialog.Contains(
                "Patch.Ncl.External.Import",
                StringComparison.Ordinal) &&
            !dialog.Contains(
                "Patch.Ncl.Corpus.UseBundled",
                StringComparison.Ordinal),
            "The only file-dialog action must be the external-source \"Choose NCL file\" button; the ambiguous generic import action must be gone.");

        Assert(
            dialog.IndexOf(
                "Command=\"{Binding PickCmd}\"",
                StringComparison.Ordinal) >
            dialog.IndexOf(
                "Style=\"{StaticResource NclExternalSourceStage}\"",
                StringComparison.Ordinal) &&
            dialog.IndexOf(
                "ItemsSource=\"{Binding Dialects}\"",
                StringComparison.Ordinal) >
            dialog.IndexOf(
                "Style=\"{StaticResource NclExternalSourceStage}\"",
                StringComparison.Ordinal),
            "Browsing for a file and choosing a dialect must live inside the external source stage, so the built-in source never opens a FileDialog and never asks for a dialect.");

        Assert(
            !dialog.Contains(
                "App\\\\Data",
                StringComparison.Ordinal) &&
            !dialog.Contains(
                "ncl.zip",
                StringComparison.OrdinalIgnoreCase) &&
            dialog.Contains(
                "Text=\"{Binding SelectedDialect.Name, Mode=OneWay}\"",
                StringComparison.Ordinal),
            "The built-in source must expose its Artemis PS3 provenance as read-only text and must never surface a corpus path.");

        Assert(
            nclVm.Contains(
                "public bool RequiresPpuRunKey =>",
                StringComparison.Ordinal) &&
            nclVm.Contains(
                "SelectedCheat is not null &&",
                StringComparison.Ordinal) &&
            nclVm.Contains(
                "IsSourceReady &&",
                StringComparison.Ordinal) &&
            dialog.Contains(
                "DataTrigger Binding=\"{Binding RequiresPpuRunKey, Mode=OneWay}\"",
                StringComparison.Ordinal),
            "PPU selection must be derived from the real cheat/source state rather than from a duplicate visibility flag.");

        Assert(
            nclVm.Contains(
                "public NclSourceChoice? SelectedSource",
                StringComparison.Ordinal) &&
            nclVm.Contains(
                "item.IsExternal == IsExternalSource",
                StringComparison.Ordinal) &&
            nclVm.Contains(
                "private void UseExternalSource()",
                StringComparison.Ordinal) &&
            nclVm.Contains(
                "UseBundledCmd.Execute(null);",
                StringComparison.Ordinal),
            "The source picker must be derived from IsExternalSource and must load the built-in corpus itself instead of requiring a separate import action.");
    }

    private static void NclOptionSelectionContract(
        string managed,
        string corpus,
        string nclVm,
        string dialog,
        string ar,
        string en)
    {
        Assert(
            managed.Contains(
                "NclVerifyStat.OptionSelectionRequired",
                StringComparison.Ordinal) &&
            managed.Contains(
                "DescribeRequiredOptions(",
                StringComparison.Ordinal) &&
            managed.Contains(
                "cheat.RequiredOptionPlaceholderIds",
                StringComparison.Ordinal),
            "Inspection must offer option cheats using only the placeholder ids the code references.");

        Assert(
            managed.Contains(
                "public bool RequiresOptionSelection =>",
                StringComparison.Ordinal) &&
            managed.Contains(
                "NclManagedPatchOptionChoice(",
                StringComparison.Ordinal),
            "A cheat choice must carry its required option sets with canonical values and labels.");

        Assert(
            nclVm.Contains(
                "ObservableCollection<NclOptionSelectorVm> OptionSelectors",
                StringComparison.Ordinal) &&
            nclVm.Contains(
                "RebuildOptionSelectors()",
                StringComparison.Ordinal),
            "The import view model must expose one selector per referenced option placeholder.");

        Assert(
            nclVm.Contains(
                "OptionSelectors.All(item => item.HasSelection)",
                StringComparison.Ordinal),
            "Preview must stay unavailable until every required option has an explicit selection.");

        Assert(
            nclVm.Contains(
                "private void OnOptionSelectionChanged()",
                StringComparison.Ordinal) &&
            nclVm.Contains(
                "private NclOptionSelection? BuildOptionSelection()",
                StringComparison.Ordinal) &&
            nclVm.Contains(
                "                        optionSelection),",
                StringComparison.Ordinal) &&
            nclVm.Contains(
                "BuildOptionSelection();",
                StringComparison.Ordinal),
            "The selected raw option values must reach the same conversion request Preview and Apply use.");

        Assert(
            nclVm.Contains(
                "DescribeBundledOptions(candidate)",
                StringComparison.Ordinal) &&
            nclVm.Contains(
                "                            optionSelection),",
                StringComparison.Ordinal) &&
            managed.Contains(
                "public sealed record NclBundledManagedPatchRequest(",
                StringComparison.Ordinal) &&
            managed.Contains(
                "OptionSelection: request.OptionSelection));",
                StringComparison.Ordinal) &&
            managed.Contains(
                "evidence.OptionSelection));",
                StringComparison.Ordinal),
            "A bundled candidate must reuse the same option selectors and carry the identical selection into bundled Preview and the bundled Apply revalidation.");

        Assert(
            corpus.Contains(
                "Discovery is identity-based, not convertibility-based.",
                StringComparison.Ordinal) &&
            corpus.Contains(
                "var canConvert = false;",
                StringComparison.Ordinal) &&
            corpus.Contains(
                "status == NclVerifyStat.OptionSelectionRequired",
                StringComparison.Ordinal) &&
            corpus.Contains(
                "candidate.CanConvert",
                StringComparison.Ordinal) &&
            corpus.Contains(
                "NclCorpusSourceRef(",
                StringComparison.Ordinal) &&
            managed.Contains(
                "if (!request.Candidate.CanConvert)",
                StringComparison.Ordinal) &&
            nclVm.Contains(
                "CanConvert: true,",
                StringComparison.Ordinal) &&
            nclVm.Contains(
                "Patch.Ncl.CheatUnavailable.RuntimeEvidenceRequired",
                StringComparison.Ordinal),
            "Bundled discovery must keep every exact-target cheat visible while Preview/Apply remain fail-closed for definitions that are not verified for offline conversion.");

        Assert(
            !nclVm.Contains(
                "Choices[0]",
                StringComparison.Ordinal) &&
            !nclVm.Contains(
                "Choices.First()",
                StringComparison.Ordinal),
            "Option selection must never default to the first declared choice.");

        Assert(
            dialog.Contains(
                "ItemsSource=\"{Binding OptionSelectors}\"",
                StringComparison.Ordinal) &&
            dialog.Contains(
                "SelectedItem=\"{Binding Selected, Mode=TwoWay}\"",
                StringComparison.Ordinal) &&
            dialog.Contains(
                "Text=\"{Binding Label}\"",
                StringComparison.Ordinal) &&
            !dialog.Contains(
                "DisplayMemberPath=\"Label\"",
                StringComparison.Ordinal),
            "The dialog must bind the option selectors and display the declared label through an item template; DisplayMemberPath and ItemTemplate must never both be set on one selector, because WPF throws when they are.");

        Assert(
            dialog.Contains(
                "<Style x:Key=\"NclOptionRowGrid\"",
                StringComparison.Ordinal) &&
            dialog.Contains(
                "Style=\"{StaticResource NclTechnicalComboBox}\"",
                StringComparison.Ordinal),
            "Option rows must reuse the existing technical direction resources instead of hard-coded layout.");

        foreach (var key in new[]
                 {
                     "\"Patch.Ncl.Options.Caption\"",
                     "\"Patch.Ncl.Options.Required\""
                 })
        {
            Assert(
                ar.Contains(key, StringComparison.Ordinal) &&
                en.Contains(key, StringComparison.Ordinal),
                $"Option selection text {key} must exist in both language packs.");
        }
    }

    // Live view-model policy against the real built-in corpus and the real
    // BCUS98232 field evidence: two logged PPU executables, only one of which
    // covers Combo Multiplier's writes.
    private const string NclSmallModuleHash =
        "1f2967e954ef4713e2d37bc8ce41ed93d17413a7";
    private const string NclMainExecutableHash =
        "3a0b43e4a5f4bfea64f53612ee7c5d990f88129c";

    private static void NclRunKeySelectionPolicy()
    {
        Exception? failure = null;
        var root = Path.Combine(
            Path.GetTempPath(),
            "NclRunKeyPolicy_" + Guid.NewGuid().ToString("N"));

        var thread = new Thread(() =>
        {
            try
            {
                var dispatcher = Dispatcher.CurrentDispatcher;
                SynchronizationContext.SetSynchronizationContext(
                    new DispatcherSynchronizationContext(dispatcher));

                var corpusPath = Path.Combine(
                    FindRepoRoot(),
                    "App",
                    "Data",
                    "ncl.zip");
                var cases = R3Config.Composition.AppComposition.Build();
                var flow = new AppFlow(cases with
                {
                    NclManagedPatch =
                        new R3Config.Application.NclManagedPatchUseCase(
                            new R3Config.Svc.BundledCheatPatchService(),
                            cases.PatchMutation,
                            corpusPath)
                });

                var fieldVm = BuildNclVm(
                    flow,
                    Path.Combine(root, "field"),
                    "0x18000",
                    "0x80e468");

                Pump(dispatcher, fieldVm.InitializeAsync());

                Assert(
                    fieldVm.SourceNoticeFragments
                        .Where(fragment => fragment.IsTechnical)
                        .Select(fragment => fragment.Text)
                        .SequenceEqual(["01.00"]) &&
                    fieldVm.SourceNotice == R3Config.Svc.TextFragments.PlainText(fieldVm.SourceNoticeFragments),
                    "NCL other-version notices must preserve canonical versions as separate technical fragments.");

                Assert(
                    fieldVm.Cheats.Count > 1 &&
                    fieldVm.SelectedCheat is null &&
                    !fieldVm.RequiresPpuRunKey &&
                    fieldVm.EligibleRunKeys.Count == 0,
                    "Several bundled cheats must open with no cheat selected and no PPU stage at all.");

                Assert(
                    fieldVm.CheatHint == R3Config.Lang.UiText.Fmt(
                        "Patch.Ncl.CheatMultiple", fieldVm.Cheats.Count),
                    "A bundled multi-cheat result must publish the localized selection hint.");

                var combo = fieldVm.Cheats.Single(item =>
                    item.Name.Equals(
                        "Combo Multiplier",
                        StringComparison.Ordinal));

                Assert(
                    fieldVm.Cheats.All(item =>
                        item.Display.Equals(
                            item.Name,
                            StringComparison.Ordinal) ||
                        item.Display.StartsWith(
                            item.Name + " (",
                            StringComparison.Ordinal)) &&
                    combo.Display.Equals(
                        "Combo Multiplier",
                        StringComparison.Ordinal),
                    "A cheat must be listed under its own name, without the internal index leaking into the display string.");

                fieldVm.SelectedCheat = combo;
                Pump(dispatcher, fieldVm.CurrentOperation);

                Assert(
                    fieldVm.OptionSelectors.Count == 1 &&
                    !fieldVm.OptionSelectors[0].HasSelection &&
                    !fieldVm.RequiresPpuRunKey &&
                    fieldVm.EligibleRunKeys.Count == 0 &&
                    fieldVm.SelectedRunKey is null,
                    "An option cheat must not default an option and must not offer a PPU executable before its options are known.");

                Assert(
                    fieldVm.OptionSelectors[0].Choices
                        .Select(item => item.Label)
                        .SequenceEqual(["10x", "100x", "1,000x"]) &&
                    fieldVm.OptionSelectors[0].Choices
                        .Select(item => item.Value)
                        .SequenceEqual(["4120", "42C8", "447A"]),
                    "Combo Multiplier must offer exactly its declared labels over its canonical raw values.");

                fieldVm.OptionSelectors[0].Selected =
                    fieldVm.OptionSelectors[0].Choices[1];
                Pump(dispatcher, fieldVm.CurrentOperation);

                Assert(
                    fieldVm.RequiresPpuRunKey &&
                    fieldVm.EligibleRunKeys.Count == 1 &&
                    IsMainExecutable(fieldVm.EligibleRunKeys[0]) &&
                    IsMainExecutable(fieldVm.SelectedRunKey),
                    "Only the logged executable whose ranges cover the writes may be offered, and being the only one it may be selected on evidence.");

                fieldVm.OptionSelectors[0].Selected =
                    fieldVm.OptionSelectors[0].Choices[0];
                Pump(dispatcher, fieldVm.CurrentOperation);

                Assert(
                    !fieldVm.HasPreview &&
                    fieldVm.EligibleRunKeys.Count == 1 &&
                    IsMainExecutable(fieldVm.SelectedRunKey),
                    "Changing an option must invalidate the preview and recompute eligibility rather than carry the previous answer over.");

                var bothVm = BuildNclVm(
                    flow,
                    Path.Combine(root, "both"),
                    "0x80e468",
                    "0x80e468");

                Pump(dispatcher, bothVm.InitializeAsync());
                SelectComboMultiplier(dispatcher, bothVm);

                Assert(
                    bothVm.EligibleRunKeys.Count == 2 &&
                    bothVm.SelectedRunKey is null &&
                    !bothVm.PreviewCmd.CanRun(null),
                    "Two evidence-valid PPU executables must require an explicit choice instead of an arbitrary one.");

                var otherKey = bothVm.EligibleRunKeys.Single(item =>
                    !IsMainExecutable(item));
                bothVm.SelectedRunKey = otherKey;

                Assert(
                    bothVm.SelectedRunKey == otherKey,
                    "An evidence-valid PPU executable must remain selectable when several qualify.");

                var noneVm = BuildNclVm(
                    flow,
                    Path.Combine(root, "none"),
                    "0x18000",
                    "0x18000");

                Pump(dispatcher, noneVm.InitializeAsync());
                SelectComboMultiplier(dispatcher, noneVm);

                noneVm.SelectedRunKey = "PPU-" + NclMainExecutableHash;

                Assert(
                    noneVm.EligibleRunKeys.Count == 0 &&
                    noneVm.SelectedRunKey is null &&
                    !noneVm.PreviewCmd.CanRun(null) &&
                    !noneVm.ApplyCmd.CanRun(null),
                    "Zero eligible PPU executables must fail closed: nothing offered, nothing selectable, nothing previewable.");

                SingleCheatExternalSourceStillAutoSelects(
                    dispatcher,
                    flow,
                    root);

                dispatcher.InvokeShutdown();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        })
        { IsBackground = true };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        try
        {
            Assert(
                thread.Join(TimeSpan.FromSeconds(120)),
                "NCL run-key selection policy test timed out.");

            if (failure is not null)
            {
                throw new InvalidOperationException(
                    "NCL run-key selection policy regression failed.",
                    failure);
            }
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    // The external path is untouched by evidence-based PPU eligibility: a file
    // with exactly one verified static cheat still auto-selects that cheat, and
    // its single covering executable is still chosen on evidence.
    private static void SingleCheatExternalSourceStillAutoSelects(
        Dispatcher dispatcher,
        AppFlow flow,
        string root)
    {
        var caseRoot = Path.Combine(root, "external");
        var nclPath = Path.Combine(
            caseRoot,
            "Single-BCUS98232-01.12.ncl");
        var vm = BuildNclVm(
            flow,
            caseRoot,
            "0x18000",
            "0x80e468",
            () => nclPath);

        File.WriteAllText(
            nclPath,
            "Only Cheat\n0\ntester\n0 00100000 60000000\n#\n",
            new System.Text.UTF8Encoding(false));

        var inspection = flow.InspectNcl(
            nclPath,
            NclDialect.ArtemisPs3,
            "BCUS98232",
            "01.12");
        Assert(
            inspection.IsOk &&
            inspection.TotalCheats == 1 &&
            inspection.StaticCheats.Count == 1 &&
            inspection.StaticCheats[0].Name == "Only Cheat",
            $"External fixture must contain exactly one verified Artemis cheat: {inspection.Code}, total={inspection.TotalCheats}, valid={inspection.StaticCheats.Count}.");

        Pump(dispatcher, vm.InitializeAsync());

        vm.SelectedSource = vm.Sources.Single(item => item.IsExternal);

        Assert(
            vm.IsExternalSource &&
            vm.Cheats.Count == 0 &&
            vm.SelectedCheat is null &&
            vm.EligibleRunKeys.Count == 0,
            "Switching to the external source must clear the bundled cheat list and every PPU answer it produced.");

        vm.PickCmd.Execute(null);
        vm.SelectedDialect = vm.Dialects.Single(item =>
            item.Value == NclDialect.ArtemisPs3);
        Pump(dispatcher, vm.CurrentOperation);

        Assert(
            vm.Cheats.Count == 1 &&
            vm.SelectedCheat?.Name == "Only Cheat" &&
            vm.SelectedCheat.Display.Equals(
                "Only Cheat",
                StringComparison.Ordinal),
            "Exactly one valid external cheat may still be auto-selected.");

        Assert(
            vm.RequiresPpuRunKey &&
            vm.EligibleRunKeys.Count == 1 &&
            IsMainExecutable(vm.EligibleRunKeys[0]) &&
            IsMainExecutable(vm.SelectedRunKey),
            "The external path must reduce PPU candidates by the same address-domain evidence as the built-in path.");

        File.AppendAllText(
            nclPath,
            "Second Cheat\n0\ntester\n0 00100004 60000000\n#\n",
            new System.Text.UTF8Encoding(false));
        vm.PickCmd.Execute(null);
        vm.SelectedDialect = vm.Dialects.Single(item =>
            item.Value == NclDialect.ArtemisPs3);
        Pump(dispatcher, vm.CurrentOperation);

        Assert(
            vm.Cheats.Count == 2 &&
            vm.SelectedCheat is null &&
            !vm.RequiresPpuRunKey &&
            vm.EligibleRunKeys.Count == 0 &&
            vm.SelectedRunKey is null,
            "Reloading multiple valid external cheats must clear the previous single-cheat selection and PPU eligibility without choosing the first item.");
        Assert(
            vm.CheatHint == R3Config.Lang.UiText.Fmt(
                "Patch.Ncl.CheatMultiple", 2),
            "An external multi-cheat result must publish the localized selection hint.");
    }

    private static bool IsMainExecutable(string? runKey)
    {
        return runKey is not null &&
               runKey.Equals(
                   "PPU-" + NclMainExecutableHash,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static void SelectComboMultiplier(
        Dispatcher dispatcher,
        NclImportVm vm)
    {
        vm.SelectedCheat = vm.Cheats.Single(item =>
            item.Name.Equals(
                "Combo Multiplier",
                StringComparison.Ordinal));
        Pump(dispatcher, vm.CurrentOperation);

        vm.OptionSelectors[0].Selected =
            vm.OptionSelectors[0].Choices[1];
        Pump(dispatcher, vm.CurrentOperation);
    }

    private static NclImportVm BuildNclVm(
        AppFlow flow,
        string caseRoot,
        string smallFileSize,
        string mainFileSize,
        Func<string?>? pickFile = null)
    {
        Directory.CreateDirectory(Path.Combine(caseRoot, "log"));

        File.WriteAllText(
            Path.Combine(caseRoot, "log", "RPCS3.log"),
            "X SYS: Serial: BCUS98232\n" +
            "X ppu_loader: ** Segment: p_type=0x1, p_vaddr=0x10000, p_filesz=" +
            smallFileSize +
            ", p_memsz=" +
            smallFileSize +
            ", flags=0x400005\n" +
            "X ppu_loader: PPU executable hash: PPU-" +
            NclSmallModuleHash +
            "\n" +
            "X ppu_loader: ** Segment: p_type=0x1, p_vaddr=0x10000, p_filesz=" +
            mainFileSize +
            ", p_memsz=" +
            mainFileSize +
            ", flags=0x400005\n" +
            "X ppu_loader: PPU executable hash: PPU-" +
            NclMainExecutableHash +
            "\n",
            new System.Text.UTF8Encoding(false));

        var install = new Rpcs3InstallDescriptor(
            caseRoot,
            Path.Combine(caseRoot, "rpcs3.exe"),
            Path.Combine(caseRoot, "config"),
            Path.Combine(caseRoot, "custom_configs"),
            Path.Combine(caseRoot, "patches"),
            Path.Combine(caseRoot, "config", "patch_config.yml"));

        var game = new Rpcs3GameDescriptor(
            "BCUS98232",
            "God Of War Ascension",
            string.Empty,
            "01.12",
            NclMainExecutableHash,
            null,
            AuthPpu:
            [
                NclSmallModuleHash,
                NclMainExecutableHash
            ]);

        return new NclImportVm(
            flow,
            install,
            game,
            game.Title,
            [],
            () => false,
            (_, _) => false,
            pickFile ?? (() => null));
    }

    private static void Pump(
        Dispatcher dispatcher,
        Task task)
    {
        if (!task.IsCompleted)
        {
            var frame = new DispatcherFrame();
            _ = task.ContinueWith(
                _ => dispatcher.BeginInvoke(
                    new Action(() => frame.Continue = false)),
                TaskScheduler.Default);
            Dispatcher.PushFrame(frame);
        }

        task.GetAwaiter().GetResult();
    }

    private static void BusyBoundaryPreservesSelectionAndCloseOwnership()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var dispatcher = Dispatcher.CurrentDispatcher;
                SynchronizationContext.SetSynchronizationContext(
                    new DispatcherSynchronizationContext(dispatcher));
                var root = Path.Combine(Path.GetTempPath(), "NclUiReadOnly_" + Guid.NewGuid().ToString("N"));
                var install = new Rpcs3InstallDescriptor(root, Path.Combine(root, "rpcs3.exe"),
                    Path.Combine(root, "config"), Path.Combine(root, "custom_configs"),
                    Path.Combine(root, "patches"), Path.Combine(root, "patch_config.yml"));
                var game = new Rpcs3GameDescriptor("BLES01128", "NCL test", string.Empty,
                    "01.02", "a2c90aafce24744d5ac6da8ad009ce301458c8a7", null);
                var vm = new NclImportVm(new AppFlow(), install, game, game.Title, [],
                    () => false, (_, _) => false, () => throw new InvalidOperationException("Busy pick executed."));
                var originalKey = vm.SelectedRunKey;
                var enteredBusy = 0;
                var closeRequests = 0;
                vm.CloseReq += (_, _) => closeRequests++;
                vm.PropChg += (_, e) =>
                {
                    if (e.PropertyName != nameof(vm.IsBusy) || !vm.IsBusy)
                    {
                        return;
                    }

                    enteredBusy++;
                    Assert(!vm.CanEdit && !vm.CloseCmd.CanRun(null) &&
                        !vm.PickCmd.CanRun(null) && !vm.UseBundledCmd.CanRun(null) &&
                        !vm.PreviewCmd.CanRun(null) && !vm.ApplyCmd.CanRun(null),
                        "NCL operations must own the source, selection and window while busy.");
                    vm.SelectedRunKey = null;
                    vm.SelectedDialect = vm.Dialects[1];
                    vm.SelectedCheat = new R3Config.Application.NclManagedPatchCheatChoice(99, "other", null, null);
                    vm.PickCmd.Execute(null);
                    vm.CloseCmd.Execute(null);
                    Assert(vm.SelectedRunKey == originalKey && vm.SelectedDialect is null && vm.SelectedCheat is null,
                        "Busy setters must preserve the operation's captured source and selection.");
                };
                var task = vm.InitializeAsync();
                var frame = new DispatcherFrame();
                _ = task.ContinueWith(_ => dispatcher.BeginInvoke(new Action(() => frame.Continue = false)),
                    TaskScheduler.Default);
                Dispatcher.PushFrame(frame);
                Assert(task.IsCompletedSuccessfully && !vm.IsBusy && vm.CanEdit &&
                    vm.CloseCmd.CanRun(null) && closeRequests == 0 && enteredBusy == 1,
                    "NCL discovery must release ownership without requesting close or mutating RPCS3.");
                Assert(ReferenceEquals(task, vm.InitializeAsync()) && enteredBusy == 1,
                    "Repeated Loaded events must reuse initialization rather than rediscover the corpus.");
                Assert(!Directory.Exists(root), "NCL discovery must remain read-only.");
                dispatcher.InvokeShutdown();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert(thread.Join(TimeSpan.FromSeconds(30)), "NCL busy lifecycle test timed out.");
        if (failure is not null)
        {
            throw new InvalidOperationException("NCL busy lifecycle regression failed.", failure);
        }
    }

    private static Dictionary<string, string> ReadNclLocalization(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement
            .EnumerateObject()
            .Where(item =>
                item.Name.StartsWith(
                    "Patch.Ncl.",
                    StringComparison.Ordinal))
            .ToDictionary(
                item => item.Name,
                item => item.Value.GetString() ?? string.Empty,
                StringComparer.Ordinal);
    }

    private static bool ContainsMojibake(string value)
    {
        if (value.Contains('Ø') || value.Contains('Ù'))
        {
            return true;
        }

        var markerHits = MojibakeMarkers
            .Where(marker => marker is not "Ø" and not "Ù")
            .Sum(marker => CountOccurrences(value, marker));

        return markerHits >= 2;
    }

    private static int CountOccurrences(
        string value,
        string marker)
    {
        var count = 0;
        var offset = 0;

        while ((offset = value.IndexOf(
                   marker,
                   offset,
                   StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += marker.Length;
        }

        return count;
    }

    private static string Read(
        string root,
        params string[] parts)
    {
        return File.ReadAllText(
            Path.Combine(
                root,
                Path.Combine(parts)));
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? current =
            new(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(
                    Path.Combine(
                        current.FullName,
                        "R3Cfg.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException(
            "R3Config repository root was not found.");
    }

    private static void Assert(
        bool condition,
        string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
