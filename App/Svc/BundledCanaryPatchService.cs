using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using R3Config.Lang;
using R3Config.Mdl;

namespace R3Config.Svc;

#pragma warning disable CA1822 // Application adapter methods remain instance based.

public sealed class BundledCanaryPatchService
{
    public const string BndlName = "can.yml";

    private readonly CanaryPatchImportService _impSvc = new();

    public static string GetPath(string baseDirectory)
    {
        return Path.Combine(baseDirectory, "Data", BndlName);
    }

    public bool Exists(string baseDirectory)
    {
        return File.Exists(GetPath(baseDirectory));
    }

    public string ReadText(string baseDirectory)
    {
        var path = GetPath(baseDirectory);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                UiText.GetText("Patch.BundledCanaryMissing"),
                path);
        }

        return R3Bound.ReadUtf8(
            path,
            R3Limits.PatCatBy,
            BndlName);
    }

    public CanStage Prep(
        string patchesPath,
        string baseDirectory,
        IReadOnlyList<PatMatch> selectedMatches)
    {
        var selectedCanary = selectedMatches
            .Where(match => match.Patch.Class == PatClass.Canary)
            .ToArray();

        if (selectedCanary.Length == 0)
        {
            return new CanStage(
                true,
                string.Empty,
                null,
                UiText.GetText("Patch.PrepareCanaryNoSelection"),
                null);
        }

        var bundledText = ReadText(baseDirectory);
        var selectedText = Build(
            bundledText,
            selectedCanary);

        if (string.IsNullOrWhiteSpace(selectedText))
        {
            return new CanStage(
                true,
                string.Empty,
                null,
                UiText.GetText("Patch.PrepareCanaryAlreadyInImported"),
                null);
        }

        return _impSvc.PrepareManagedImportedPatchFile(
            patchesPath,
            selectedText);
    }

    private static string Build(
        string bundledText,
        IReadOnlyList<PatMatch> selectedMatches)
    {
        // Compile selected Canary patches into a self-contained YAML document
        // by expanding all anchors and aliases before staging imported_patch.yml.
        var compiled = new PatchSelectionCompiler().Compile(
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
