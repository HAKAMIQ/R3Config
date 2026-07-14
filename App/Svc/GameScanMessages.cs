using System.IO;
using R3Config.Lang;
using R3Config.Mdl;

namespace R3Config.Svc;

public sealed class LocalizedGameScanDiagnosticFormatter
{
    public string Format(GameScanDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);

        return UiText.F(
            $"ScanDiag.{diagnostic.Code}",
            SafeSourceName(diagnostic));
    }

    private static string SafeSourceName(
        GameScanDiagnostic diagnostic)
    {
        if (string.IsNullOrWhiteSpace(diagnostic.SourcePath))
        {
            return diagnostic.SourceKind.ToString();
        }

        var normalized = Path.TrimEndingDirectorySeparator(
            diagnostic.SourcePath);
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
            ? diagnostic.SourceKind.ToString()
            : name;
    }
}
