using System.IO;
using System.Linq;
using R3Config.Mdl;

#pragma warning disable IDE0130

namespace R3Config.Svc;

public sealed class PlanService
{
    private readonly RecStateService _recStateService = new();

    public ConfigPlan CreateConfigPlan(
        Rpcs3InstallDescriptor install,
        Rpcs3GameDescriptor game,
        WikiGameRecommendation? recommendation,
        HwInfo? hw = null)
    {
        var targetPath = Path.Combine(
            install.CustomConfigsPath,
            $"config_{game.Serial}.yml");

        var changes = (recommendation?.Changes ?? [])
            .OrderBy(change => change.Section)
            .ThenBy(change => change.Key)
            .ToArray();

        WikiGameRecommendation[] recs = recommendation is null
            ? []
            : [recommendation with { Changes = changes }];

        var canReadConfig = Rpcs3ConfigYaml.CanRead(targetPath);

        var state = _recStateService.Evaluate(
            game,
            recs,
            (section, key) =>
            {
                if (!canReadConfig)
                {
                    throw new InvalidDataException(
                        "Config file is not readable.");
                }

                return Rpcs3ConfigYaml.ReadValue(
                    targetPath,
                    section,
                    key);
            },
            hw);

        var items = changes
            .Zip(
                state.Items,
                (change, stateItem) =>
                    new ConfigPlanEntry(
                        change,
                        stateItem.Current,
                        stateItem))
            .ToArray();

        return new ConfigPlan(
            GameSerial: game.Serial,
            GameTitle: recommendation?.WikiTitle ?? game.Title,
            TargetConfigPath: targetPath,
            Items: items,
            RecState: state,
            RequiresBackup: items.Any(
                item => item.IsActionable && item.IsDifferent));
    }
}

#pragma warning restore IDE0130