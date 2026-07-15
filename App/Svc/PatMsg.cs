using R3Config.Lang;
using R3Config.Application;

namespace R3Config.Svc;

public sealed class LocalizedPatchCatalogMessageFormatter :
    IPatchCatalogMessageFormatter
{
    public string MissCat =>
        UiText.GetText("Patch.FileMissing");

    public string BadCat =>
        UiText.GetText("Patch.FileInvalid");

    public string FmtRead(
        string? rawError,
        bool isImported)
    {
        if (isImported && IsDupKey(rawError))
        {
            return UiText.GetText("Patch.ImportedDuplicateKey");
        }

        if (!isImported && IsDupKey(rawError))
        {
            return UiText.GetText("Patch.Localized.DuplicateKey");
        }

        var fallback = isImported
            ? UiText.GetText("Patch.ImportedFileInvalid")
            : UiText.GetText("Patch.FileInvalid");
        var detail = string.IsNullOrWhiteSpace(rawError)
            ? fallback
            : rawError.Trim();

        return UiText.Fmt(
            isImported
                ? "Patch.ImportedReadWarning"
                : "Patch.OfficialReadWarning",
            detail);
    }

    private static bool IsDupKey(
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
