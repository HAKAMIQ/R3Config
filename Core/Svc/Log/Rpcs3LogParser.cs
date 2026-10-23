using System;
using System.Threading;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using R3Config.Mdl;

#pragma warning disable IDE0130

namespace R3Config.Svc;

// Pure, read-only parser of one RPCS3 log into a LogSession. It sees only the
// lines it is given; it never opens a file, never modifies anything and never
// keeps the whole log (only structured events, the timeline and a bounded set
// of raw lines).
public static partial class Rpcs3LogParser
{
    public const int MaxEvents = 500;
    public const int MaxRawLines = 400;
    public const int MaxSettings = 500;
    private const int ContinuationLines = 3;

    // "·<level> H:MM:SS.ffffff {thread} channel: message"
    private static readonly Regex Line = LineRegex();

    [GeneratedRegex(
        @"^﻿?·(?<level>[A-Za-z!])\s(?<time>\d+:\d{2}:\d{2}(?:\.\d+)?)\s(?:\{(?<thread>.*?)\}\s)?(?<channel>[A-Za-z_][A-Za-z0-9_ ]*?):\s?(?<message>.*)$",
        RegexOptions.CultureInvariant)]
    private static partial Regex LineRegex();

    private static readonly Regex Hash = HashRegex();

    [GeneratedRegex(@"PPU executable hash:\s*PPU-(?<hash>[0-9a-fA-F]{40})", RegexOptions.CultureInvariant)]
    private static partial Regex HashRegex();

    private static readonly Regex GpuFound = GpuRegex();

    [GeneratedRegex(@"Found Vulkan-compatible GPU:\s*'(?<gpu>[^']+)'", RegexOptions.CultureInvariant)]
    private static partial Regex GpuRegex();

    private static readonly Regex Initialized = InitRegex();

    [GeneratedRegex(@"^(?<api>Vulkan|OpenGL|GL): Renderer initialized on device '(?<gpu>[^']*)'", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex InitRegex();

    private static readonly Regex AppVer = AppVerRegex();

    [GeneratedRegex(@"APP_VER=(?<v>[0-9][0-9.]*)", RegexOptions.CultureInvariant)]
    private static partial Regex AppVerRegex();

    private static readonly Regex Firmware = FirmwareRegex();

    [GeneratedRegex(@"^Firmware version:\s*(?<v>\d+(?:\.\d+)*)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex FirmwareRegex();

    private static readonly Regex VulkanGpu = VulkanGpuRegex();

    [GeneratedRegex(@"Found Vulkan-compatible GPU:\s*'(?<gpu>[^']+)'\s+running on driver\s+(?<driver>\S+)", RegexOptions.CultureInvariant)]
    private static partial Regex VulkanGpuRegex();

    private static readonly Regex PackageVer = PackageVerRegex();

    [GeneratedRegex(@"\bVERSION=(?<v>[0-9][0-9.]*)", RegexOptions.CultureInvariant)]
    private static partial Regex PackageVerRegex();

    private static readonly Regex ConfigMode = ConfigModeRegex();

    [GeneratedRegex(@"^Selected config:\s*mode=(?<mode>\w+) config(?:,\s*path=""(?<path>[^""]*)"")?", RegexOptions.CultureInvariant)]
    private static partial Regex ConfigModeRegex();

    private static readonly Regex IsoEnc = IsoEncRegex();

    [GeneratedRegex(@"^init: Set 'enc type':\s*(?<enc>[A-Za-z0-9_]+)", RegexOptions.CultureInvariant)]
    private static partial Regex IsoEncRegex();

    private static readonly Regex Applied = AppliedRegex();

    [GeneratedRegex(@"^Applied patch \(hash='(?<hash>[^']+)', description='(?<desc>.*?)', author=", RegexOptions.CultureInvariant)]
    private static partial Regex AppliedRegex();

    private const int MaxAppliedPatches = 100;
    private const int MaxObservations = 300;
    private const int MaxSamples = 20000;
    private const int MaxWin32Errors = 50;
    private const int MaxDiscPaths = 500;

    private static readonly Regex CompatDb = CompatDbRegex();

    // RPCS3 prints where its compat database lives, which is inside the data
    // folder of the run: "Compat: Finished reading database from file: <path>".
    [GeneratedRegex(@"^(?:Finished reading database from file|Database file not found): (?<path>.+?)[/\\]GuiConfigs[/\\]compat_database\.dat", RegexOptions.CultureInvariant)]
    private static partial Regex CompatDbRegex();

    private static readonly Regex FsFailure = FsFailureRegex();

    [GeneratedRegex(@"^'(?:sys_fs_open|sys_fs_stat|sys_fs_opendir)' failed with 0x[0-9a-fA-F]+ : CELL_\w+, [“""](?<path>/dev_bdvd/[^”""]+)[”""]", RegexOptions.CultureInvariant)]
    private static partial Regex FsFailureRegex();

    // Bump when the shape of what the parser extracts changes.
    public const string ParserVersion = "2";

    private static readonly Regex Threads = ThreadsRegex();

    [GeneratedRegex(@"\|\s*\d+\s+Threads\s*\|", RegexOptions.CultureInvariant)]
    private static partial Regex ThreadsRegex();

    public static LogSession Parse(
        IEnumerable<string> lines,
        LogSourceInfo? source = null,
        string? preferSerial = null,
        bool truncated = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lines);
        cancellationToken.ThrowIfCancellationRequested();

        var machine = new Machine();
        foreach (var raw in lines)
        {
            cancellationToken.ThrowIfCancellationRequested();
            machine.Feed(raw);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return machine.Finish(source, preferSerial, truncated);
    }

    // Everything one boot of a game contributes.
    private sealed class Segment
    {
        public string? Serial, Title, AppVersion, Category, Renderer, Gpu;
        public readonly List<string> Hashes = [];
        public readonly List<LogSetting> Settings = [];
        public readonly List<LogEvent> Events = [];
        public readonly Dictionary<string, int> BySignature = [];
        public readonly List<LogTimelineStep> Timeline = [];
        public readonly HashSet<string> Stages = [];
        public readonly List<string> Raw = [];
        public readonly List<LogAppliedPatch> Patches = [];
        public readonly List<LogObservation> Observations = [];
        public readonly Dictionary<(LogObservationKind, string?), int> ObservationIndex = [];
        public readonly List<Win32ErrorObservation> Win32 = [];
        public readonly SortedSet<string> DiscPaths = new(StringComparer.Ordinal);
        public readonly List<double> CpuSamples = [];
        public readonly List<int> RamSamples = [];
        public int RamPeak;
        public string? PackageVersion, ConfigMode, IsoEncryption;
        public int ConfigBlocks;
        public bool CustomConfigFile, RendererFromConfig;
        public LogBootMedia BootMedia;
        public LogIsoKeyState IsoKey;
        public bool RendererInitialized;
    }

    private sealed record Pending(char Level, string Channel, string? Thread, double Seconds, int Line);

    private sealed class Machine
    {
        private readonly List<string> _header = [];
        private readonly List<Segment> _segments = [new()];
        private readonly List<(int Indent, string Name)> _section = [];
        private Segment _current;
        private string? _cpu, _os, _processGpu, _firmware, _dataPath;
        private readonly Dictionary<string, string> _gpuDrivers = new(StringComparer.Ordinal);
        private int _line, _continuation;
        private bool _inConfig;
        private LogEvent? _last;
        private Pending? _pending;

        public Machine()
        {
            _current = _segments[0];
        }

        public void Feed(string raw)
        {
            _line++;
            var text = raw.TrimEnd('\r');
            var match = Line.Match(text);

            if (!match.Success)
            {
                FeedContinuation(text);
                return;
            }

            FlushPending(null);
            _inConfig = false;
            _continuation = 0;
            _last = null;

            var level = match.Groups["level"].Value[0];
            var channel = match.Groups["channel"].Value.Trim();
            var message = match.Groups["message"].Value;
            var seconds = ParseSeconds(match.Groups["time"].Value);
            var thread = match.Groups["thread"].Success ? match.Groups["thread"].Value : null;

            // RPCS3 prints some errors as an empty header line followed by the
            // text; the event is completed by the next unprefixed line.
            if (message.Trim().Length == 0 && level is 'E' or 'F' or 'W')
            {
                _pending = new Pending(level, channel, thread, seconds, _line);
                return;
            }

            Process(level, channel, thread, seconds, _line, message, text);
        }

        private void FeedContinuation(string text)
        {
            if (_pending is not null && text.Trim().Length > 0)
            {
                var pending = _pending;
                _pending = null;
                Process(pending.Level, pending.Channel, pending.Thread, pending.Seconds, pending.Line, text.Trim(), text);
                return;
            }

            if (_header.Count < 8 && _current.Events.Count == 0 && _line <= 12 && !_inConfig)
            {
                _header.Add(text);
                _cpu ??= Threads.IsMatch(text) ? text.Trim() : null;
                _os ??= text.StartsWith("Operating system:", StringComparison.Ordinal) ? text.Trim() : null;
            }

            if (_inConfig)
            {
                ParseSettingLine(text);
            }
            else if (_last is not null && _continuation > 0 && _last.IsProblem && _current.Raw.Count < MaxRawLines && text.Length > 0)
            {
                _current.Raw.Add($"{_line}: {Cut(text)}");
                _continuation--;
            }
        }

        private void FlushPending(string? message)
        {
            if (_pending is null)
            {
                return;
            }

            var pending = _pending;
            _pending = null;
            if (message is not null)
            {
                Process(pending.Level, pending.Channel, pending.Thread, pending.Seconds, pending.Line, message, message);
            }
        }

        private void Process(char level, string channel, string? thread, double seconds, int line, string message, string rawText)
        {
            if (channel == "SYS" && message.StartsWith("Emulator::BootGame", StringComparison.Ordinal))
            {
                _current = new Segment();
                _segments.Add(_current);
                AddStage("GameBoot", line, seconds, null);
            }
            else if (channel == "SYS" && message.StartsWith("Serial:", StringComparison.Ordinal) && _current.Serial is not null &&
                     !string.Equals(_current.Serial, message["Serial:".Length..].Trim(), StringComparison.Ordinal))
            {
                // A different serial without a BootGame line is another boot.
                _current = new Segment();
                _segments.Add(_current);
            }

            Extract(channel, message, line, seconds);
            Observe(level, channel, message, line);

            var categories = LogRules.Classify(level, channel, message);
            if (categories.Count == 0)
            {
                return;
            }

            var signature = categories.Contains(LogCategory.GameBoot) || categories.Contains(LogCategory.GameExit)
                ? null
                : LogRules.BuildSignature(level, channel, message);
            if (categories.Contains(LogCategory.GameExit))
            {
                AddStage("GameExit", line, seconds, Cut(message));
            }

            if (signature is not null && _current.BySignature.TryGetValue(signature.Id, out var index))
            {
                var existing = _current.Events[index];
                _current.Events[index] = existing with { LastLine = line, Count = existing.Count + 1 };
                return;
            }

            if (_current.Events.Count >= MaxEvents)
            {
                return;
            }

            var created = new LogEvent(line, line, seconds, level, thread, channel, Cut(message), categories, signature);
            if (signature is not null)
            {
                _current.BySignature[signature.Id] = _current.Events.Count;
            }

            _current.Events.Add(created);
            if (!created.IsProblem)
            {
                return;
            }

            if (!created.IsMinor)
            {
                AddStage("Problem:" + created.Primary, line, seconds, Cut(message));
            }

            if (_current.Raw.Count < MaxRawLines)
            {
                _current.Raw.Add($"{line}: {Cut(rawText)}");
                _last = created;
                _continuation = ContinuationLines;
            }
        }

        private void Observe(char level, string channel, string message, int line)
        {
            if (LogObservationRules.TryMatch(level, channel, message, out var kind, out var value))
            {
                var key = (kind, value);
                if (_current.ObservationIndex.TryGetValue(key, out var index))
                {
                    var existing = _current.Observations[index];
                    _current.Observations[index] = existing with { Count = existing.Count + 1, LastLine = line };
                }
                else if (_current.Observations.Count < MaxObservations)
                {
                    _current.ObservationIndex[key] = _current.Observations.Count;
                    _current.Observations.Add(new LogObservation(kind, value, level, 1, line, line));
                }
            }

            if (LogObservationRules.TryMatchPerf(channel, message, out var cpu, out var ram, out var peak) &&
                _current.CpuSamples.Count < MaxSamples)
            {
                _current.CpuSamples.Add(cpu);
                if (ram is { } used && peak is { } max)
                {
                    _current.RamSamples.Add(used);
                    _current.RamPeak = Math.Max(_current.RamPeak, max);
                }
            }

            if (_dataPath is null && channel == "Compat" && CompatDb.Match(message) is { Success: true } compat)
            {
                _dataPath = compat.Groups["path"].Value;
            }

            if (level == 'E' && channel == "sys_fs" && _current.DiscPaths.Count < MaxDiscPaths &&
                FsFailure.Match(message) is { Success: true } failure)
            {
                _current.DiscPaths.Add(failure.Groups["path"].Value["/dev_bdvd/".Length..]);
            }

            if (_current.Win32.Count < MaxWin32Errors &&
                message.Contains("(error=", StringComparison.Ordinal) &&
                LogObservationRules.TryMatchWin32(message, out var code, out var text, out var raw))
            {
                _current.Win32.Add(new Win32ErrorObservation(
                    code,
                    Win32ErrorSymbols.Lookup(code),
                    text,
                    raw.Length > 200 ? raw[..200] : raw,
                    line));
            }
        }

        private static LogPerformanceSummary? Summarize(Segment segment)
        {
            if (segment.CpuSamples.Count == 0)
            {
                return null;
            }

            var cpu = segment.CpuSamples.Order().ToArray();
            static double Percentile(double[] sorted, double p) => sorted[(int)Math.Clamp(Math.Round(p * (sorted.Length - 1)), 0, sorted.Length - 1)];
            var ram = segment.RamSamples.Order().ToArray();
            return new LogPerformanceSummary(
                cpu.Length,
                cpu.Average(),
                Percentile(cpu, 0.5),
                Percentile(cpu, 0.05),
                Percentile(cpu, 0.95),
                cpu[0],
                cpu[^1],
                ram.Length,
                ram.Length == 0 ? null : ram[ram.Length / 2],
                segment.RamPeak == 0 ? null : segment.RamPeak);
        }

        private void Extract(string channel, string message, int line, double seconds)
        {
            switch (channel)
            {
                case "SYS":
                    if (message.StartsWith("Title:", StringComparison.Ordinal))
                    {
                        _current.Title = message["Title:".Length..].Trim();
                    }
                    else if (message.StartsWith("Serial:", StringComparison.Ordinal))
                    {
                        _current.Serial = message["Serial:".Length..].Trim();
                        AddStage("GameIdentified", line, seconds, _current.Serial);
                    }
                    else if (message.StartsWith("Category:", StringComparison.Ordinal))
                    {
                        _current.Category = message["Category:".Length..].Trim();
                    }
                    else if (message.StartsWith("Version:", StringComparison.Ordinal) && AppVer.Match(message) is { Success: true } version)
                    {
                        _current.AppVersion = version.Groups["v"].Value;
                        _current.PackageVersion = PackageVer.Match(message) is { Success: true } package ? package.Groups["v"].Value : null;
                    }
                    else if (Firmware.Match(message) is { Success: true } firmware)
                    {
                        _firmware ??= firmware.Groups["v"].Value;
                    }
                    else if (ConfigMode.Match(message) is { Success: true } mode)
                    {
                        _current.ConfigMode = mode.Groups["mode"].Value;
                        _current.CustomConfigFile |= mode.Groups["path"].Value.Length > 0;
                    }
                    else if (message.StartsWith("Loading iso archive", StringComparison.Ordinal))
                    {
                        _current.BootMedia = LogBootMedia.IsoArchive;
                    }
                    else if (message.StartsWith("Used configuration:", StringComparison.Ordinal))
                    {
                        // RPCS3 prints this block again after a custom config is
                        // applied; the last block is the effective configuration,
                        // so an earlier one is replaced, never merged.
                        _inConfig = true;
                        _section.Clear();
                        _current.Settings.Clear();
                        _current.ConfigBlocks++;
                        AddStage("ConfigurationLoaded", line, seconds, null);
                    }

                    break;

                case "ppu_loader":
                    if (Hash.Match(message) is { Success: true } hash)
                    {
                        var value = hash.Groups["hash"].Value.ToLowerInvariant();
                        if (!_current.Hashes.Contains(value))
                        {
                            _current.Hashes.Add(value);
                        }

                        AddStage("PpuInitialization", line, seconds, null);
                    }

                    break;

                case "SPU":
                    AddStage("SpuActivity", line, seconds, null);
                    break;

                case "ISO":
                    if (message.StartsWith("init: Failed to open, or missing, key file", StringComparison.Ordinal))
                    {
                        _current.IsoKey = LogIsoKeyState.Missing;
                    }
                    else if (message.StartsWith("init: Found matching key file", StringComparison.Ordinal))
                    {
                        _current.IsoKey = LogIsoKeyState.Found;
                    }
                    else if (IsoEnc.Match(message) is { Success: true } enc)
                    {
                        _current.IsoEncryption = enc.Groups["enc"].Value;
                    }

                    break;

                case "PAT":
                    if (Applied.Match(message) is { Success: true } patch && _current.Patches.Count < MaxAppliedPatches)
                    {
                        var applied = new LogAppliedPatch(patch.Groups["hash"].Value, patch.Groups["desc"].Value);
                        if (!_current.Patches.Contains(applied))
                        {
                            _current.Patches.Add(applied);
                        }
                    }

                    break;

                case "RSX":
                    if (GpuFound.Match(message) is { Success: true } gpu)
                    {
                        _processGpu ??= gpu.Groups["gpu"].Value;
                    }

                    if (VulkanGpu.Match(message) is { Success: true } vulkan)
                    {
                        _gpuDrivers.TryAdd(vulkan.Groups["gpu"].Value, vulkan.Groups["driver"].Value);
                    }

                    if (Initialized.Match(message) is { Success: true } init)
                    {
                        _current.RendererFromConfig = false;
                        _current.Renderer = init.Groups["api"].Value.Equals("GL", StringComparison.OrdinalIgnoreCase)
                            ? "OpenGL"
                            : init.Groups["api"].Value;
                        _current.Gpu = init.Groups["gpu"].Value;
                        _current.RendererInitialized = true;
                        AddStage("RendererInitialization", line, seconds, _current.Renderer);
                    }

                    break;

                case "GUI":
                    if (message.StartsWith("Boot successful", StringComparison.Ordinal))
                    {
                        AddStage("BootSuccessful", line, seconds, null);
                    }

                    break;
            }
        }

        private void ParseSettingLine(string text)
        {
            if (text.Trim().Length == 0 || _current.Settings.Count >= MaxSettings)
            {
                return;
            }

            var indent = text.Length - text.TrimStart().Length;
            var body = text.Trim();
            var colon = body.IndexOf(':', StringComparison.Ordinal);
            if (colon <= 0)
            {
                return;
            }

            var name = body[..colon].Trim();
            var value = body[(colon + 1)..].Trim();
            while (_section.Count > 0 && _section[^1].Indent >= indent)
            {
                _section.RemoveAt(_section.Count - 1);
            }

            if (value.Length == 0)
            {
                _section.Add((indent, name));
                return;
            }

            var path = string.Join('/', _section.Select(item => item.Name).Append(name));
            _current.Settings.Add(new LogSetting(path, value));
            if ((_current.Renderer is null || _current.RendererFromConfig) && path == "Video/Renderer")
            {
                _current.Renderer = value;
                _current.RendererFromConfig = true;
            }
        }

        private void AddStage(string stage, int line, double seconds, string? detail)
        {
            // First occurrence of each stage; problem stages once per category.
            if (_current.Stages.Add(stage))
            {
                _current.Timeline.Add(new LogTimelineStep(stage, line, seconds, detail));
            }
        }

        public LogSession Finish(LogSourceInfo? source, string? preferSerial, bool truncated)
        {
            FlushPending(null);
            var segment = Choose(preferSerial);
            var build = Rpcs3BuildIdentityParser.TryParse(_header, out var banner) ? banner : null;
            var events = segment.Events;

            // Synthesized only from log evidence: the game booted but the
            // renderer never reported initialization, and a graphics-side
            // problem was logged.
            var graphics = events.FirstOrDefault(item => item.Categories.Any(category => category is
                LogCategory.RsxError or LogCategory.VulkanError or LogCategory.OpenglError or LogCategory.RenderingError));
            // A fragment with no header may simply have cut off before the renderer
            // initialized, so the absence of that line proves nothing there.
            if (build is not null && segment.Serial is not null && !segment.RendererInitialized && graphics is not null)
            {
                events =
                [
                    .. events,
                    new LogEvent(
                        graphics.Line, graphics.Line, graphics.Seconds, graphics.Level, null, graphics.Channel,
                        "The game booted, a graphics error was logged and the renderer never reported initialization.",
                        [LogCategory.BlackScreenIndicator], null)
                ];
            }

            return new LogSession(
                source,
                build,
                _cpu,
                _os,
                segment.Gpu ?? _processGpu,
                segment.Renderer,
                segment.Serial,
                segment.Title,
                segment.AppVersion,
                segment.Category,
                [.. segment.Hashes],
                [.. segment.Settings],
                events,
                [.. segment.Timeline.OrderBy(step => step.Line)],
                [.. segment.Raw],
                _line,
                _segments.Count(item => item.Serial is not null),
                truncated,
                new LogRunContext(
                    _firmware,
                    // The driver of the adapter that initialized the renderer,
                    // by exact name; never the first or "the" driver.
                    segment.Gpu is not null && _gpuDrivers.TryGetValue(segment.Gpu, out var driver) ? driver : null,
                    segment.PackageVersion,
                    segment.ConfigMode,
                    segment.BootMedia,
                    segment.IsoKey,
                    segment.IsoEncryption,
                    [.. segment.Patches],
                    segment.ConfigBlocks,
                    segment.CustomConfigFile,
                    [.. segment.Observations],
                    [.. segment.Win32],
                    Summarize(segment),
                    truncated || build is null
                        ? LogCompleteness.Partial
                        : segment.Serial is not null && segment.Stages.Contains("GameExit")
                            ? LogCompleteness.Complete
                            : LogCompleteness.Unknown,
                    [.. segment.DiscPaths],
                    _dataPath,
                    _dataPath is null ? null : [.. InstallPathTraits.Of(_dataPath)]));
        }

        private Segment Choose(string? preferSerial)
        {
            if (!string.IsNullOrWhiteSpace(preferSerial))
            {
                var wanted = _segments.LastOrDefault(item =>
                    string.Equals(item.Serial, preferSerial.Trim(), StringComparison.OrdinalIgnoreCase));
                if (wanted is not null)
                {
                    return wanted;
                }
            }

            return _segments.LastOrDefault(item => item.Serial is not null) ?? _segments[0];
        }
    }

    private static double ParseSeconds(string value)
    {
        var parts = value.Split(':');
        if (parts.Length != 3 ||
            !double.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var hours) ||
            !double.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) ||
            !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
        {
            return 0;
        }

        return hours * 3600 + minutes * 60 + seconds;
    }

    private static string Cut(string value) => value.Length > 300 ? value[..300] : value;
}

#pragma warning restore IDE0130
