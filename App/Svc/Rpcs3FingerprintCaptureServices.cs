using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using R3Config.Application;

namespace R3Config.Svc;

public sealed class Rpcs3OwnedProcessController
    : IRpcs3OwnedProcessController
{
    private const int MinimumClientWidth = 320;
    private const int MinimumClientHeight = 180;

    private static readonly TimeSpan StartTimeTolerance =
        TimeSpan.FromMilliseconds(500);

    public bool TryAttach(
        string executablePath,
        int processId,
        out DateTime processStartedUtc)
    {
        processStartedUtc = default;
        if (!TryOpenLaunched(
                executablePath,
                processId,
                out var process))
        {
            return false;
        }

        using (process)
        {
            try
            {
                processStartedUtc =
                    process.StartTime.ToUniversalTime();
                return true;
            }
            catch (Exception ex) when (
                ex is InvalidOperationException or
                Win32Exception or
                NotSupportedException)
            {
                return false;
            }
        }
    }

    public bool IsRunning(
        string executablePath,
        int processId,
        DateTime processStartedUtc)
    {
        if (!TryOpenOwned(
                executablePath,
                processId,
                processStartedUtc,
                out var process))
        {
            return false;
        }

        using (process)
        {
            try
            {
                return !process.HasExited;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }

    public bool IsGameStartupReady(
        string executablePath,
        int processId,
        DateTime processStartedUtc)
    {
        if (!TryOpenOwned(
                executablePath,
                processId,
                processStartedUtc,
                out var process))
        {
            return false;
        }

        using (process)
        {
            try
            {
                process.Refresh();
                if (process.HasExited || !process.Responding)
                {
                    return false;
                }

                var window = process.MainWindowHandle;
                if (window == IntPtr.Zero ||
                    !IsWindowVisible(window) ||
                    IsIconic(window))
                {
                    return false;
                }

                _ = GetWindowThreadProcessId(
                    window,
                    out var windowProcessId);
                if (windowProcessId != (uint)processId)
                {
                    return false;
                }

                if (!GetClientRect(window, out var clientRect))
                {
                    return false;
                }

                return clientRect.Width >= MinimumClientWidth &&
                       clientRect.Height >= MinimumClientHeight;
            }
            catch (Exception ex) when (
                ex is InvalidOperationException or
                Win32Exception or
                NotSupportedException)
            {
                return false;
            }
        }
    }

    public async Task<Rpcs3OwnedProcessStopResult> StopAsync(
        string executablePath,
        int processId,
        DateTime launchedUtc,
        TimeSpan gracefulTimeout,
        CancellationToken cancellationToken)
    {
        if (!TryOpenOwned(
                executablePath,
                processId,
                launchedUtc,
                out var process))
        {
            return new Rpcs3OwnedProcessStopResult(
                Stopped: true);
        }

        using (process)
        {
            try
            {
                if (process.HasExited)
                {
                    return new Rpcs3OwnedProcessStopResult(
                        Stopped: true);
                }

                var closeRequested = process.CloseMainWindow();
                if (closeRequested &&
                    await WaitForExitAsync(
                        process,
                        gracefulTimeout,
                        cancellationToken))
                {
                    return new Rpcs3OwnedProcessStopResult(
                        Stopped: true);
                }

                process.Refresh();
                if (process.HasExited)
                {
                    return new Rpcs3OwnedProcessStopResult(
                        Stopped: true);
                }

                process.Kill(entireProcessTree: true);
                var forcedExit = await WaitForExitAsync(
                    process,
                    TimeSpan.FromSeconds(5),
                    cancellationToken);

                return forcedExit
                    ? new Rpcs3OwnedProcessStopResult(
                        Stopped: true,
                        Forced: true)
                    : new Rpcs3OwnedProcessStopResult(
                        Stopped: false,
                        Forced: true,
                        Error: "RPCS3 did not exit after the owned process tree was terminated.");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (
                ex is ArgumentException or
                InvalidOperationException or
                Win32Exception or
                NotSupportedException)
            {
                return new Rpcs3OwnedProcessStopResult(
                    Stopped: false,
                    Error: ex.Message);
            }
        }
    }

    private static async Task<bool> WaitForExitAsync(
        Process process,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (process.HasExited)
        {
            return true;
        }

        using var timeoutSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
        timeoutSource.CancelAfter(timeout);

        try
        {
            await process.WaitForExitAsync(timeoutSource.Token);
            return true;
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return process.HasExited;
        }
    }

    private static bool TryOpenOwned(
        string executablePath,
        int processId,
        DateTime processStartedUtc,
        out Process process)
    {
        process = null!;
        if (!TryOpenLaunched(
                executablePath,
                processId,
                out var candidate))
        {
            return false;
        }

        try
        {
            var actualStartedUtc =
                candidate.StartTime.ToUniversalTime();
            if ((actualStartedUtc - processStartedUtc).Duration() >
                StartTimeTolerance)
            {
                candidate.Dispose();
                return false;
            }

            process = candidate;
            return true;
        }
        catch (Exception ex) when (
            ex is InvalidOperationException or
            Win32Exception or
            NotSupportedException)
        {
            candidate.Dispose();
            return false;
        }
    }

    private static bool TryOpenLaunched(
        string executablePath,
        int processId,
        out Process process)
    {
        process = null!;
        if (processId <= 0 || string.IsNullOrWhiteSpace(executablePath))
        {
            return false;
        }

        try
        {
            var expectedPath = Path.GetFullPath(executablePath);
            var candidate = Process.GetProcessById(processId);
            if (candidate.HasExited ||
                !MatchesExecutablePath(candidate, expectedPath))
            {
                candidate.Dispose();
                return false;
            }

            process = candidate;
            return true;
        }
        catch (Exception ex) when (
            ex is ArgumentException or
            InvalidOperationException or
            Win32Exception or
            NotSupportedException or
            PathTooLongException)
        {
            return false;
        }
    }

    private static bool MatchesExecutablePath(
        Process candidate,
        string expectedPath)
    {
        try
        {
            var actualPath = candidate.MainModule?.FileName;
            return !string.IsNullOrWhiteSpace(actualPath) &&
                   Path.GetFullPath(actualPath).Equals(
                       expectedPath,
                       StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (
            ex is InvalidOperationException or
            Win32Exception or
            NotSupportedException or
            ArgumentException)
        {
            return true;
        }
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr hWnd);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(
        IntPtr hWnd,
        out uint processId);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(
        IntPtr hWnd,
        out Rect rect);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public readonly int Width => Math.Max(0, Right - Left);

        public readonly int Height => Math.Max(0, Bottom - Top);
    }
}
