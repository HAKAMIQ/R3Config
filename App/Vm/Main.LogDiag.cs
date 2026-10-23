using System;
using System.Threading;
using System.Threading.Tasks;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Vm;

// Log Diagnostics of Domain 5: read-only, on-demand and cancellable. The result
// is published only if the install and the selected game are still the ones it
// was started for, so a stale read never shows another game's log.
public sealed partial class MainVm
{
    private RuntimeVm? _runtime;
    private DiagnosticKnowledgeBase? _diagKnowledge;
    private string? _diagKnowledgeError;
    private CancellationTokenSource? _logDiagCancellation;
    private long _logDiagVersion;
    private string? _logDiagKey;

    // Domain 5: Runtime Environment & Compatibility (evidence only).
    public RuntimeVm Runtime => _runtime ??= new RuntimeVm(
        new LogDiagVm(new AsyncCmd(RunLogDiagAsync, CanRunLogDiag)));

    private bool CanRunLogDiag() => !_disposed && _install is not null;

    private void ScheduleLogDiag()
    {
        var serial = GameList.SelGame?.Game.Serial;
        if (_install is null || serial is null)
        {
            CancelLogDiag();
            _logDiagKey = null;
            Runtime.LogDiag.Clear();
            return;
        }

        var key = $"{_install.RootPath}|{serial}";
        if (string.Equals(key, _logDiagKey, StringComparison.Ordinal))
        {
            return;
        }

        _logDiagKey = key;
        _ = RunLogDiagAsync();
    }

    private void CancelLogDiag()
    {
        var previous = Interlocked.Exchange(ref _logDiagCancellation, null);
        previous?.Cancel();
        previous?.Dispose();
        Interlocked.Increment(ref _logDiagVersion);
    }

    private async Task RunLogDiagAsync()
    {
        var install = _install;
        var serial = GameList.SelGame?.Game.Serial;
        if (install is null || _disposed)
        {
            return;
        }

        CancelLogDiag();
        var version = Interlocked.Read(ref _logDiagVersion);
        var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        _logDiagCancellation = cancellation;
        var hardware = _hwInfo;
        var build = _buildIdentity;

        _diagKnowledge ??= _flow.LoadDiagnosticsKnowledge(AppContext.BaseDirectory, out _diagKnowledgeError);
        var knowledge = _diagKnowledge;
        var knowledgeError = _diagKnowledgeError;
        Runtime.LogDiag.SetContext(Runtime.Snapshot, _rpcs3Builds, Rpcs3BuildCatalog.WindowsPlatform);
        Runtime.LogDiag.MarkRunning();

        try
        {
            var result = await Task.Run(
                () => _flow.AnalyzeLogs(install, serial, hardware, build, knowledge, knowledgeError, token),
                token);

            if (_disposed ||
                version != Interlocked.Read(ref _logDiagVersion) ||
                !ReferenceEquals(install, _install) ||
                !string.Equals(serial, GameList.SelGame?.Game.Serial, StringComparison.Ordinal))
            {
                return;
            }

            Runtime.LogDiag.SetContext(Runtime.Snapshot, _rpcs3Builds, Rpcs3BuildCatalog.WindowsPlatform);
            Runtime.LogDiag.Load(result);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (!_disposed && version == Interlocked.Read(ref _logDiagVersion))
            {
                Runtime.LogDiag.Clear();
                Log(UiText.Fmt("Msg.LogDiagFailed", ex.Message));
            }
        }
    }
}
