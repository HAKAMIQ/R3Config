namespace R3Config.Case;

public sealed record AppCases(
    AnlzCase Analysis,
    CfgCase Config,
    PatCat PatCat,
    PatMut PatMut,
    CanImp CanImp,
    RecCat RecCat,
    BkpCase Backup,
    HCanCase HCanary);
