using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using R3Config.Lang;
using R3Config.Mdl;

namespace R3Config.Svc;

public sealed class BundledCanaryPatchService
{
    public const string BundledFileName = "can.yml";

    private readonly CanaryPatchImportService _importService = new();

    public static string GetBundledPath(string baseDirectory)
    {
        return Path.Combine(baseDirectory, "Data", BundledFileName);
    }

    public bool Exists(string baseDirectory)
    {
        return File.Exists(GetBundledPath(baseDirectory));
    }

    public string ReadText(string baseDirectory)
    {
        var path = GetBundledPath(baseDirectory);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                UiText.T("Patch.BundledCanaryMissing"),
                path);
        }

        return File.ReadAllText(path, Encoding.UTF8);
    }

    public CanaryPatchStageResult PrepareImportedPatchFile(
        string patchesPath,
        string baseDirectory,
        IReadOnlyList<PatMatch> selectedMatches)
    {
        var selectedCanary = selectedMatches
            .Where(match => match.Patch.Risk == PatRisk.Canary)
            .ToArray();

        if (selectedCanary.Length == 0)
        {
            return new CanaryPatchStageResult(
                true,
                string.Empty,
                null,
                UiText.T("Patch.PrepareCanaryNoSelection"),
                null);
        }

        var bundledText = ReadText(baseDirectory);
        var selectedText = BuildSelectedCanaryText(
            bundledText,
            selectedCanary);

        if (string.IsNullOrWhiteSpace(selectedText))
        {
            return new CanaryPatchStageResult(
                true,
                string.Empty,
                null,
                UiText.T("Patch.PrepareCanaryAlreadyInImported"),
                null);
        }

        return _importService.PrepareManagedImportedPatchFile(
            patchesPath,
            selectedText);
    }

    private static string BuildSelectedCanaryText(
        string bundledText,
        IReadOnlyList<PatMatch> selectedMatches)
    {
        // Compile the selection into a self-contained RPCS3 document. Merely
        // copying the selected hash block leaves aliases such as Games/Notes
        // pointing at the top-level Anchors section. The managed imported-file
        // merger intentionally keeps only executable hash sections, so those
        // aliases become dangling and RPCS3/YamlDotNet rejects the result.
        // PatchSelectionCompiler clones the selected entries and expands every
        // alias before the imported_patch.yml transaction is staged.
        var compiled = new SelComp().Compile(
            bundledText,
            selectedMatches,
            selectedMatches);

        if (!compiled.IsOk)
        {
            throw new InvalidDataException(
                compiled.Error ??
                "The selected Canary patches could not be compiled.");
        }

        return compiled.PatText;
    }
}
