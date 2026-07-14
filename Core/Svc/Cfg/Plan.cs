using System.IO;
using System.Linq;
using R3Config.Mdl;

#pragma warning disable IDE0130

namespace R3Config.Svc;

public sealed class PlanSvc
{
    private readonly StateSvc _recSvc = new();

    public CfgPlan BuildCfg(
        R3Inst install,
        R3Game game,
        WikiRec? recommendation,
        HwInfo? hw = null)
    {
        var targetPath = Path.Combine(
            install.CustPath,
            $"config_{game.Serial}.yml");

        var changes = (recommendation?.Changes ?? [])
            .OrderBy(change => change.Section)
            .ThenBy(change => change.Key)
            .ToArray();

        WikiRec[] recs = recommendation is null
            ? []
            : [recommendation with { Changes = changes }];

        var canReadConfig = R3Yaml.CanRead(targetPath);

        var state = _recSvc.Evaluate(
            game,
            recs,
            (section, key) =>
            {
                if (!canReadConfig)
                {
                    throw new InvalidDataException(
                        "Config file is not readable.");
                }

                return R3Yaml.ReadVal(
                    targetPath,
                    section,
                    key);
            },
            hw);

        var items = changes
            .Zip(
                state.Items,
                (change, stateItem) =>
                    new CfgEntry(
                        change,
                        stateItem.Current,
                        stateItem))
            .ToArray();

        return new CfgPlan(
            GameSer: game.Serial,
            GameName: recommendation?.WikiName ?? game.Title,
            CfgPath: targetPath,
            Items: items,
            RecState: state,
            NeedBkp: items.Any(
                item => item.IsAction && item.IsDiff));
    }
}

#pragma warning restore IDE0130