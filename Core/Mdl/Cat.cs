namespace R3Config.Mdl;

public sealed record CatInfo(
    string DataSrc,
    string DataVer,
    DateTimeOffset? GenAt,
    string SrcHash,
    int RecNum)
{
    public string Display => string.IsNullOrWhiteSpace(DataVer)
        ? $"Data: {DataSrc}"
        : $"Data: {DataSrc} {DataVer}";

    public string RecText => $"Recommendations: {RecNum}";
}
