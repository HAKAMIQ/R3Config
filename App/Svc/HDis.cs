using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using R3Config.Application;
using R3Config.Mdl;

namespace R3Config.Svc;

public sealed class Rpcs3ProcessProbe : IRpcs3ProcessProbe
{
    private readonly Func<string, Process[]> _getProcesses;

    public Rpcs3ProcessProbe()
        : this(Process.GetProcessesByName)
    {
    }

    internal Rpcs3ProcessProbe(
        Func<string, Process[]> getProcesses)
    {
        _getProcesses = getProcesses ??
            throw new ArgumentNullException(nameof(getProcesses));
    }

    public Rpcs3ProcessState Find(
        string executablePath,
        int? expectedProcessId = null)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return new Rpcs3ProcessState(false);
        }

        string expectedPath;
        try
        {
            expectedPath = Path.GetFullPath(executablePath);
        }
        catch (Exception ex) when (
            ex is ArgumentException or
            NotSupportedException or
            PathTooLongException)
        {
            return new Rpcs3ProcessState(false);
        }

        if (expectedProcessId is > 0)
        {
            try
            {
                using var expected = Process.GetProcessById(
                    expectedProcessId.Value);

                if (Matches(
                        expected,
                        expectedPath,
                        trustProcessIdOnAccessFailure: true))
                {
                    return new Rpcs3ProcessState(
                        true,
                        expected.Id);
                }
            }
            catch (Exception ex) when (
                ex is ArgumentException or
                InvalidOperationException or
                System.ComponentModel.Win32Exception or
                NotSupportedException)
            {
            }
        }

        Process[] processes;
        try
        {
            processes = _getProcesses(
                Path.GetFileNameWithoutExtension(expectedPath));
        }
        catch (Exception ex) when (
            ex is InvalidOperationException or
            System.ComponentModel.Win32Exception or
            NotSupportedException)
        {
            return new Rpcs3ProcessState(false);
        }

        try
        {
            foreach (var process in processes)
            {
                if (Matches(
                        process,
                        expectedPath,
                        trustProcessIdOnAccessFailure: false))
                {
                    return new Rpcs3ProcessState(
                        true,
                        process.Id);
                }
            }
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }

        return new Rpcs3ProcessState(false);
    }

    private static bool Matches(
        Process process,
        string expectedPath,
        bool trustProcessIdOnAccessFailure)
    {
        try
        {
            if (process.HasExited)
            {
                return false;
            }

            var actualPath = process.MainModule?.FileName;

            return !string.IsNullOrWhiteSpace(actualPath) &&
                   Path.GetFullPath(actualPath).Equals(
                       expectedPath,
                       StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (
            ex is InvalidOperationException or
            System.ComponentModel.Win32Exception or
            NotSupportedException or
            ArgumentException)
        {
            return trustProcessIdOnAccessFailure;
        }
    }
}

public sealed partial class HCanDiscoveryStore : IHCanDiscoveryStore
{
    private const int SchemaVersion = 1;
    private const int MaxBindings = 512;
    private const int MaxHashesPerKind = 128;
    private const int MaxVersionChars = 64;
    private const int MaxPathChars = 32_767;
    private const string DirectoryName = "R3Config";
    private const string FileName = "hdis.json";
    private const string MutexPrefix = @"Local\R3Config.HCanDiscoveryStore.";
    private const long MaxStoreBytes = 4L * 1024 * 1024;

    private static readonly TimeSpan MutexWaitTimeout =
        TimeSpan.FromSeconds(10);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        MaxDepth = 32
    };

    private readonly string _path;
    private readonly string _mutexName;

    public HCanDiscoveryStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        _path = Path.GetFullPath(path);
        _mutexName = BuildMutexName(_path);
    }

    public HCanDiscoveryStore()
        : this(Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            DirectoryName,
            FileName))
    {
    }

    public HCanDiscoverySession? LoadActive()
    {
        return ExecuteLocked(() => Load().Active);
    }

    public void SaveActive(HCanDiscoverySession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var normalized = NormalizeSession(session) ??
                         throw new ArgumentException(
                             "H-Canary discovery session is invalid.",
                             nameof(session));

        ExecuteLocked(() =>
        {
            var state = Load();

            Save(state with
            {
                Active = normalized
            });
        });
    }

    public void ClearActive()
    {
        ExecuteLocked(() =>
        {
            var state = Load();

            if (state.Active is null)
            {
                return;
            }

            Save(state with
            {
                Active = null
            });
        });
    }

    public HCanHashBinding? LoadBinding(
        string rpcs3ExecutablePath,
        string serial,
        string? gameVersion,
        string gameInstallPath)
    {
        if (!TryNormalizeKey(
                rpcs3ExecutablePath,
                serial,
                gameVersion,
                gameInstallPath,
                out var key))
        {
            return null;
        }

        return ExecuteLocked(() => Load().Bindings.FirstOrDefault(binding =>
            SameKey(binding, key)));
    }

    public void SaveBinding(
        HCanHashBinding binding,
        bool replaceExisting = false)
    {
        ArgumentNullException.ThrowIfNull(binding);

        var normalized = NormalizeBinding(binding) ??
                         throw new ArgumentException(
                             "H-Canary hash binding is invalid.",
                             nameof(binding));

        ExecuteLocked(() =>
        {
            var state = Load();

            var existing = state.Bindings.FirstOrDefault(item =>
                SameKey(item, normalized));

            if (existing?.DetectedPpuHashes.Count > 0 &&
                !replaceExisting)
            {
                return;
            }

            var bindings = state.Bindings
                .Where(item => !SameKey(item, normalized))
                .Append(normalized)
                .OrderByDescending(item => item.DetectedUtc)
                .Take(MaxBindings)
                .ToArray();

            Save(state with
            {
                Bindings = bindings
            });
        });
    }

    public bool DeleteBinding(
        string rpcs3ExecutablePath,
        string serial,
        string? gameVersion,
        string gameInstallPath)
    {
        if (!TryNormalizeKey(
                rpcs3ExecutablePath,
                serial,
                gameVersion,
                gameInstallPath,
                out var key))
        {
            return false;
        }

        return ExecuteLocked(() =>
        {
            var state = Load();

            var bindings = state.Bindings
                .Where(item => !SameKey(item, key))
                .ToArray();

            if (bindings.Length == state.Bindings.Length)
            {
                return false;
            }

            Save(state with
            {
                Bindings = bindings
            });

            return true;
        });
    }

    private T ExecuteLocked<T>(Func<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        using var mutex = new Mutex(
            initiallyOwned: false,
            name: _mutexName);
        var acquired = false;

        try
        {
            try
            {
                acquired = mutex.WaitOne(MutexWaitTimeout);
            }
            catch (AbandonedMutexException)
            {
                acquired = true;
                Trace.TraceWarning(
                    "H-Canary discovery store recovered an abandoned cross-process lock.");
            }

            if (!acquired)
            {
                throw new IOException(
                    "Timed out waiting for the H-Canary discovery store lock.");
            }

            return action();
        }
        finally
        {
            if (acquired)
            {
                mutex.ReleaseMutex();
            }
        }
    }

    private void ExecuteLocked(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        ExecuteLocked(() =>
        {
            action();
            return true;
        });
    }

    private StoreState Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return StoreState.Empty;
            }

            var json = R3Bound.ReadUtf8(
                _path,
                MaxStoreBytes,
                "H-Canary discovery store");

            var state = JsonSerializer.Deserialize<StoreState>(
                json,
                JsonOptions);

            return NormalizeState(state);
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            InvalidDataException or
            DecoderFallbackException or
            JsonException or
            NotSupportedException)
        {
            Trace.TraceWarning(
                $"H-Canary discovery store load failed: {ex.Message}");

            return StoreState.Empty;
        }
    }

    private void Save(StoreState state)
    {
        string? tempPath = null;

        try
        {
            var normalized = NormalizeState(state with
            {
                Schema = SchemaVersion
            });
            var directory = Path.GetDirectoryName(_path);

            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new InvalidOperationException(
                    "H-Canary discovery store directory is unavailable.");
            }

            Directory.CreateDirectory(directory);

            var destinationName = Path.GetFileName(_path);
            if (string.IsNullOrWhiteSpace(destinationName))
            {
                throw new InvalidOperationException(
                    "H-Canary discovery store file name is unavailable.");
            }

            tempPath = Path.Combine(
                directory,
                $".{destinationName}.{Guid.NewGuid():N}.tmp");

            using (var stream = new FileStream(
                       tempPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None))
            {
                JsonSerializer.Serialize(
                    stream,
                    normalized,
                    JsonOptions);

                stream.Flush(flushToDisk: true);

                if (stream.Length > MaxStoreBytes)
                {
                    throw new InvalidDataException(
                        $"H-Canary discovery store exceeds the {MaxStoreBytes}-byte output limit.");
                }
            }

            File.Move(
                tempPath,
                _path,
                overwrite: true);

            tempPath = null;
        }
        finally
        {
            if (tempPath is not null)
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch (Exception ex) when (
                    ex is IOException or
                    UnauthorizedAccessException)
                {
                    Trace.TraceWarning(
                        $"H-Canary discovery store cleanup failed: {ex.Message}");
                }
            }
        }
    }
}
