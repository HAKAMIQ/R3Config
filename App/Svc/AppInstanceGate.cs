using System;
using System.Threading;

namespace R3Config.Svc;

public sealed class AppInstanceGate : IDisposable
{
    private const string MutexName =
        @"Local\HAKAMIQ.R3Config.SingleInstance";

    private readonly Mutex _mutex;
    private bool _ownsMutex;

    private AppInstanceGate(
        Mutex mutex,
        bool ownsMutex)
    {
        _mutex = mutex;
        _ownsMutex = ownsMutex;
    }

    public static bool TryAcquire(
        out AppInstanceGate? gate)
    {
        var mutex = new Mutex(
            initiallyOwned: false,
            MutexName);

        bool acquired;

        try
        {
            acquired = mutex.WaitOne(
                millisecondsTimeout: 0,
                exitContext: false);
        }
        catch (AbandonedMutexException)
        {
            acquired = true;
        }

        if (!acquired)
        {
            mutex.Dispose();
            gate = null;
            return false;
        }

        gate = new AppInstanceGate(
            mutex,
            ownsMutex: true);

        return true;
    }

    public void Dispose()
    {
        if (_ownsMutex)
        {
            _ownsMutex = false;
            _mutex.ReleaseMutex();
        }

        _mutex.Dispose();
        GC.SuppressFinalize(this);
    }
}