using R3Config.Lang;
using R3Config.Case;

namespace R3Config.Svc;

public sealed class PatMsgFm :
    IPatMsg
{
    public string MissCat =>
        UiText.T("Patch.FileMissing");

    public string BadCat =>
        UiText.T("Patch.FileInvalid");

    public string FmtRead(
        string? rawError,
        bool isImported)
    {
        if (isImported && IsDupKey(rawError))
        {
            return UiText.T("Patch.ImportedDuplicateKey");
        }

        if (!isImported && IsDupKey(rawError))
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
