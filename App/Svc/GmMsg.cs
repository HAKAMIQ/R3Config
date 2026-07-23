using System;
using System.IO;
using R3Config.Lang;
using R3Config.Mdl;

namespace R3Config.Svc;

public sealed class GmDiag
{
    public string Format(GameScanDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);

        return UiText.Fmt(
            $"ScanDiag.{diagnostic.Code}",
            SrcName(diagnostic));
    }

    private static string SrcName(
        GameScanDiagnostic diagnostic)
    {
        if (string.IsNullOrWhiteSpace(diagnostic.SrcPath))
        {
            return diagnostic.SrcKind.ToString();
        }

        var normalized = Path.TrimEndingDirectorySeparator(
            diagnostic.SrcPath);

        var name = Path.GetFileName(normalized);

        if (name.Equals(
                "PARAM.SFO",
                StringComparison.OrdinalIgnoreCase))
        {
            var parent = Path.GetFileName(
                Path.GetDirectoryName(normalized));

            return string.IsNullOrWhiteSpace(parent)
                ? name
                : $"{parent}/{name}";
        }

        return string.IsNullOrWhiteSpace(name)
            ? diagnostic.SrcKind.ToString()
            : name;
    }
}