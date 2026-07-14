using R3Config.Lang;
using R3Config.UseCases;

namespace R3Config.Svc;

public sealed class LocalizedPatchCatalogMessageFormatter :
    IPatchCatalogMessageFormatter
{
    public string MissingCatalog =>
        UiText.T("Patch.FileMissing");

    public string InvalidCatalog =>
        UiText.T("Patch.FileInvalid");

    public string FormatReadError(
        string? rawError,
        bool isImported)
    {
        if (isImported && IsDuplicateKeyError(rawError))
        {
            return UiText.T("Patch.ImportedDuplicateKey");
        }

        if (!isImported && IsDuplicateKeyError(rawError))
        {
            return UiText.T("Patch.Localized.DuplicateKey");
        }

        var fallback = isImported
            ? UiText.T("Patch.ImportedFileInvalid")
            : UiText.T("Patch.FileInvalid");
        var detail = string.IsNullOrWhiteSpace(rawError)
            ? fallback
            : rawError.Trim();

        return UiText.F(
            isImported
                ? "Patch.ImportedReadWarning"
                : "Patch.OfficialReadWarning",
            detail);
    }

    private static bool IsDuplicateKeyError(
        string? error)
    {
        var value = error ?? string.Empty;

        return Contains(value, "duplicate key") ||
               Contains(value, "duplicated key") ||
               Contains(value, "already contains an entry");
    }

    private static bool Contains(
        string value,
        string fragment)
    {
        return value.Contains(
            fragment,
            StringComparison.OrdinalIgnoreCase);
    }
}
