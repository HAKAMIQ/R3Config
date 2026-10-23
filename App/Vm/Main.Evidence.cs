using System;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Vm;

// Wiki notes and online/RPCN evidence for the selected game. The sidecar is
// optional and read-only; the settings list itself never depends on it.
public sealed partial class MainVm
{
    private WikiEvidenceCatalog? _wikiEvidence;
    private Rpcs3BuildCatalog? _rpcs3Builds;
    private DiagnosticEvidenceCatalog? _diagnosticEvidence;
    private Rpcs3KnowledgeCatalog? _knowledge;
    private GameSettingsModel? _gameSettings;

    private GameSettingsModel ProjectGameSettings(
        GameVm selected,
        CfgPlan plan)
    {
        if (_wikiEvidence is null)
        {
            var result = _flow.LoadWikiEvidence(AppContext.BaseDirectory);
            _wikiEvidence = result.Catalog;

            if (result.Error is not null)
            {
                Log(UiText.Fmt("Msg.WikiNotesLoadFailed", result.Error));
            }
        }

        _rpcs3Builds ??= _flow.LoadRpcs3Builds(AppContext.BaseDirectory);

        LoadKnowledge();

        _gameSettings = _flow.GameSettings(
            plan,
            selected.Game,
            selected.Rec,
            _wikiEvidence,
            _buildIdentity,
            _hwInfo,
            _rpcs3Builds,
            _knowledge);
        return _gameSettings;
    }

    // The compact catalog is optional read-only evidence for the selected game.
    private void LoadKnowledge()
    {
        if (_knowledge is null)
        {
            var knowledge = _flow.LoadKnowledge(AppContext.BaseDirectory);
            _knowledge = knowledge.Catalog;

            if (knowledge.Error is not null)
            {
                Log(UiText.Fmt("Msg.KnowledgeRejected", knowledge.Error));
            }
        }
    }

    // Domain 5 snapshot of the machine, the RPCS3 install/build and the
    // selected game, from the evidence the scanners already collected.
    private void RefreshRuntimeContext()
    {
        _rpcs3Builds ??= _flow.LoadRpcs3Builds(AppContext.BaseDirectory);
        if (_diagnosticEvidence is null)
        {
            var evidence = _flow.LoadDiagnosticEvidence(AppContext.BaseDirectory);
            _diagnosticEvidence = evidence.Catalog;

            if (evidence.Error is not null)
            {
                Log(UiText.Fmt("Msg.DiagnosticEvidenceRejected", evidence.Error));
            }
        }

        var game = _install is null ? null : GameList.SelGame?.Game;
        Runtime.Load(_flow.RuntimeContext(
            _hwInfo,
            _install,
            _buildIdentity,
            game,
            game is null ? null : _gameSettings,
            _rpcs3Builds,
            _diagnosticEvidence));
        ScheduleLogDiag();
    }
}
