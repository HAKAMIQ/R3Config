using System;
using System.Threading;

namespace R3Config.Vm;

public sealed partial class MainVm
{
    private readonly object _operationSync = new();
    private CancellationTokenSource? _analysisCancellation;
    private CancellationTokenSource? _fingerprintCancellation;
    private CancellationTokenSource? _gameLibraryScanCancellation;
    private long _analysisVersion;
    private long _fingerprintVersion;
    private long _gameLibraryScanVersion;
    private bool _disposed;

    public void Dispose()
    {
        lock (_operationSync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _analysisVersion++;
            _fingerprintVersion++;
            _gameLibraryScanVersion++;
            _analysisCancellation?.Cancel();
            _fingerprintCancellation?.Cancel();
            _gameLibraryScanCancellation?.Cancel();
            _analysisCancellation = null;
            _fingerprintCancellation = null;
            _gameLibraryScanCancellation = null;
        }

        CancelLogDiag();
        StopRecSync();
        HCanary.HashChg -= OnHCanaryHashChanged;
        if (IsBusy)
        {
            IsBusy = false;
        }
        if (IsFingerprintCaptureRunning)
        {
            IsFingerprintCaptureRunning = false;
            ResetFingerprintCaptureState();
        }
        AnlzCmd.RaiseCan();
        R3Cmd.RaiseCan();
        RaiseCmd();
        HCanary.Dispose();
        GC.SuppressFinalize(this);
    }
}
