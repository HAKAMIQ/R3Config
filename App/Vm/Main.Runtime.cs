using System;
using System.Threading;

namespace R3Config.Vm;

public sealed partial class MainVm
{
    private readonly object _operationSync = new();
    private CancellationTokenSource? _analysisCancellation;
    private CancellationTokenSource? _fingerprintCancellation;
    private long _analysisVersion;
    private long _fingerprintVersion;
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
            _analysisCancellation?.Cancel();
            _fingerprintCancellation?.Cancel();
            _analysisCancellation = null;
            _fingerprintCancellation = null;
        }

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
