namespace R3Config.Mdl;

public enum NclOptionResolveCode
{
    Success,
    InvalidInput,
    AmbiguousOptionSet,
    UnknownPlaceholder,
    SelectionRequired,
    InvalidSelection,
    SubstitutionLimit
}

public sealed record NclOptionResolveResult(
    bool IsOk,
    NclOptionResolveCode Code,
    string ResolvedText,
    IReadOnlyList<int> RequiredPlaceholderIds,
    IReadOnlyList<int> MissingPlaceholderIds,
    int SubstitutionCount,
    string? Error);

// An explicit, per-cheat option choice. This travels inside the conversion
// request rather than living in global mutable state, so Preview and the Apply
// revalidation resolve the same source with exactly the same values.
public sealed record NclOptionSelection(
    int CheatIndex,
    IReadOnlyDictionary<int, string> ValuesByPlaceholderId);
