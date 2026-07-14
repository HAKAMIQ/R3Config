namespace R3Config.Case;

public sealed record AppCases(
    AnlzCase Analysis,
    CfgCase Config,
    PatCat PatchCatalog,
    PatMut PatchMutation,
    CanImp CanaryImport,
    RecCat RecommendationCatalog,
    BkpCase Backup,
    HCanCase HCanary);
