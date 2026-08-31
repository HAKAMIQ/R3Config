namespace R3Config.Mdl;

public enum PatDepMode
{
    All,
    Any
}

public sealed record PatDepGroup(
    PatDepMode Mode,
    IReadOnlyList<string> Titles);

public sealed record ArtDepRule(
    string RunKey,
    string Serial,
    string Version,
    string PatchTitle,
    PatDepMode Mode,
    IReadOnlyList<string> Requires,
    string Evidence);

public sealed record ArtDepCat(
    string SchemaVersion,
    string SourceIdentity,
    string SourceRevision,
    string ArtemisSha256,
    IReadOnlyList<ArtDepRule> Rules);

public sealed record ArtDepProjection(
    PatCat Catalog,
    int RuleCount,
    int SourceCount);
