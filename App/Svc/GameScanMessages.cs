using System.IO;
using R3Config.Lang;
using R3Config.Mdl;

namespace R3Config.Svc;

public sealed class LocalizedGameScanDiagnosticFormatter
{
    public string Format(ScanDiag diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);

        return UiText.F(
            $"ScanDiag.{diagnostic.Code}",
            SafeSourceName(diagnostic));
    }

    private static string SafeSourceName(
        ScanDiag diagnostic)
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
