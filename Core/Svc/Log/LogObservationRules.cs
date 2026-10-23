using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using R3Config.Mdl;

#pragma warning disable IDE0130

namespace R3Config.Svc;

// Typed observations for explicit RPCS3 log lines. Every rule is anchored to
// the exact level, channel and message format RPCS3 itself prints; Source names
// the RPCS3 source line the format comes from (rpcs3 source tree, log level
// letters from rpcs3/util/logs.cpp: F fatal, E error, W warning, ! notice,
// S success, U todo). A rule records what the line says; it never says why it
// happened and never suggests a fix.
public static class LogObservationRules
{
    private const RegexOptions Opts = RegexOptions.Compiled | RegexOptions.CultureInvariant;

    public sealed record Rule(LogObservationKind Kind, string Levels, string Channel, Regex Message, string Source, string Evidence);

    private static Rule R(LogObservationKind kind, string levels, string channel, string message, string source, string evidence) =>
        new(kind, levels, channel, new Regex(message, Opts), source, evidence);

    // Evidence: RealLog = a line of this exact form exists in the local real-log
    // corpus; RpcsSource = the format is taken from the RPCS3 source only.
    public static readonly Rule[] Rules =
    [
        R(LogObservationKind.FirmwareNotInstalled, "E", "ppu_loader",
            @"^PS3 firmware is not installed or the installed firmware is invalid", "rpcs3/Emu/Cell/PPUModule.cpp:2707", "RpcsSource"),
        R(LogObservationKind.FirmwareCertificateMissing, "E", "cellSsl",
            @"^Can't find certificate file (?<v>.+?), do you have the PS3 firmware installed\?", "rpcs3/Emu/Cell/Modules/cellSsl.cpp:99", "RpcsSource"),
        R(LogObservationKind.PatchFileLoadFailed, "F", "PAT",
            @"^Failed to load patch file (?<v>.+?):?\s*$", "Utilities/bin_patch.cpp:186", "RpcsSource"),
        R(LogObservationKind.IsoCorrupt, "E", "ISO",
            @"^iso_archive: Corrupt ISO file '[^']*': (?<v>.+)$", "rpcs3/Loader/ISO.cpp:1124", "RpcsSource"),
        R(LogObservationKind.InvalidFileFormat, "E", "SYS",
            @"^Invalid or unsupported file format: (?<v>.+)$", "rpcs3/Emu/System.cpp:2760", "RealLog"),
        R(LogObservationKind.NpDrmVerifyFailed, "E", "sceNp",
            @"^npDrmIsAvailable\(\): Failed to verify sce file [“""](?<v>.+?)[”""]\s*$", "rpcs3/Emu/Cell/Modules/sceNp.cpp:684", "RpcsSource"),
        R(LogObservationKind.EdatDecryptFailed, "E", "SYS",
            @"^verify_c00_unlock_edat\(\): Failed to decrypt '(?<v>[^']*)'", "rpcs3/Emu/system_utils.cpp:490", "RpcsSource"),
        R(LogObservationKind.EdatBlockInvalidHash, "E", "EDAT",
            @"^Block at offset (?<v>0x[0-9a-fA-F]+) has invalid hash!", "rpcs3/Crypto/unedat.cpp:321", "RpcsSource"),
        R(LogObservationKind.RapNotFound, "E", "sceNp",
            @"^npDrmIsAvailable\(\): Rap file not found: '(?<v>[^']*)'", "rpcs3/Emu/Cell/Modules/sceNp.cpp:717", "RpcsSource"),
        R(LogObservationKind.GameLicenseNotFound, "F", "SELF",
            @"^Failed to locate the game license file: (?<v>.+?)\.\s*$", "rpcs3/Crypto/unself.cpp:1325", "RpcsSource"),
        R(LogObservationKind.GpuFeatureUnsupported, "E", "RSX",
            @"^Your GPU does not support (?<v>[^.]+)\.", "rpcs3/Emu/RSX/VK/vkutils/device.cpp:644", "RealLog"),
        R(LogObservationKind.DriverFeatureLacking, "!", "RSX",
            @"^GPU/driver lacks support for (?<v>[^.]+)\.", "rpcs3/Emu/RSX/VK/vkutils/device.cpp:747", "RealLog"),
        R(LogObservationKind.UnsupportedDevice, "W", "RSX",
            @"^Unsupported device: (?<v>.+)$", "rpcs3/Emu/RSX/VK/VKHelpers.cpp:153", "RpcsSource"),
        R(LogObservationKind.DescriptorPoolFragmentation, "W", "RSX",
            @"^Descriptor pool creation failed with fragmentation error", "rpcs3/Emu/RSX/VK/vkutils/descriptors.cpp:286", "RpcsSource"),
        R(LogObservationKind.PadBindFailed, "E", "GUI",
            @"^gui_pad_thread: Failed to bind device '(?<v>[^']*)' to handler", "rpcs3/Input/gui_pad_thread.cpp:173", "RpcsSource"),
        R(LogObservationKind.AudioBackendInitFailed, "E", "SYS",
            @"^Audio renderer (?<v>.+?) could not be initialized, using a Null renderer instead", "rpcs3/main_application.cpp:240", "RpcsSource"),
        R(LogObservationKind.UnimplementedSyscall, "U", "PPU TODO",
            @"^Unimplemented syscall (?<v>\S+) -> CELL_OK", "rpcs3/Emu/Cell/lv2/lv2.cpp:116", "RealLog"),
        R(LogObservationKind.TrophyInstallFailed, "E", "sceNpTrophy",
            @"^sceNpTrophyRegisterContext\(\): Failed to install trophy context '(?<v>[^']*)'", "rpcs3/Emu/Cell/Modules/sceNpTrophy.cpp:712", "RpcsSource"),
        R(LogObservationKind.SaveDataRemoveFailed, "F", "SYS",
            @"^Failed to remove save data backup: (?<v>.+?) \(", "rpcs3/Emu/System.cpp:779", "RpcsSource"),
        R(LogObservationKind.ShaderCompileFailed, "F", "RSX",
            @"^Compilation failed: (?<v>[^\r\n]+)", "rpcs3/Emu/RSX/GL/glutils/program.cpp:201", "RpcsSource"),
        R(LogObservationKind.ShaderLinkFailed, "F", "RSX",
            @"^Linkage failed: (?<v>[^\r\n]+)", "rpcs3/Emu/RSX/GL/glutils/program.cpp:283", "RpcsSource"),
        R(LogObservationKind.TitleRenamedOnBoot, "!", "SYS",
            @"^Title was set from (?<v>.+)$", "rpcs3/Emu/System.cpp:2436", "RealLog")
    ];

    // Stable identity of the rule table, part of the analysis manifest.
    public static string Fingerprint { get; } = Compute();

    private static string Compute()
    {
        var builder = new StringBuilder();
        foreach (var rule in Rules)
        {
            builder.Append(rule.Kind).Append('|').Append(rule.Levels).Append('|').Append(rule.Channel).Append('|').Append(rule.Message).Append('\n');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())))[..12].ToLowerInvariant();
    }

    public static bool TryMatch(char level, string channel, string message, out LogObservationKind kind, out string? value)
    {
        foreach (var rule in Rules)
        {
            if (!rule.Levels.Contains(level, StringComparison.Ordinal) ||
                !string.Equals(rule.Channel, channel, StringComparison.Ordinal))
            {
                continue;
            }

            var match = rule.Message.Match(message);
            if (!match.Success)
            {
                continue;
            }

            kind = rule.Kind;
            var raw = match.Groups["v"].Success ? match.Groups["v"].Value.Trim() : null;
            value = raw is null ? null : FileNameOrText(raw, rule.Kind);
            return true;
        }

        kind = default;
        value = null;
        return false;
    }

    // Path-valued observations keep the file name only (the directory says
    // nothing about the event and is the user's private layout).
    private static string FileNameOrText(string raw, LogObservationKind kind)
    {
        if (kind is not (LogObservationKind.PatchFileLoadFailed or LogObservationKind.InvalidFileFormat or
            LogObservationKind.NpDrmVerifyFailed or LogObservationKind.EdatDecryptFailed or LogObservationKind.RapNotFound or
            LogObservationKind.GameLicenseNotFound or LogObservationKind.FirmwareCertificateMissing or LogObservationKind.TrophyInstallFailed or
            LogObservationKind.SaveDataRemoveFailed))
        {
            return raw.Length > 200 ? raw[..200] : raw;
        }

        var text = raw.Replace('\\', '/').TrimEnd('/');
        var slash = text.LastIndexOf('/');
        var name = slash >= 0 ? text[(slash + 1)..] : text;
        return name.Length == 0 ? raw : name.Length > 200 ? name[..200] : name;
    }

    // ---------------------------------------------------------- performance

    private static readonly Regex Perf = new(
        @"^CPU Usage: Total: (?<cpu>\d+(?:\.\d+)?)%(?:.*?, RAM Usage: (?<ram>\d+)MB \(Peak: (?<peak>\d+)MB\))?",
        Opts);

    // "CPU Usage: Total: 72.5%, Cores: ..., RAM Usage: 1234MB (Peak: 2345MB)"
    // (rpcs3/Emu/perf_monitor.cpp:73/87), channel PERF.
    public static bool TryMatchPerf(string channel, string message, out double cpu, out int? ramMb, out int? peakMb)
    {
        cpu = 0;
        ramMb = null;
        peakMb = null;
        if (!string.Equals(channel, "PERF", StringComparison.Ordinal))
        {
            return false;
        }

        var match = Perf.Match(message);
        if (!match.Success ||
            !double.TryParse(match.Groups["cpu"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out cpu))
        {
            return false;
        }

        if (match.Groups["ram"].Success &&
            int.TryParse(match.Groups["ram"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var ram) &&
            int.TryParse(match.Groups["peak"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var peak))
        {
            ramMb = ram;
            peakMb = peak;
        }

        return true;
    }

    // ---------------------------------------------------------------- Win32

    private static readonly Regex Win32 = new(
        @"\(error=(?:(?<text>.*?)\s*\(0x(?<c1>[0-9a-fA-F]{1,8})\)|0x(?<c2>[0-9a-fA-F]{1,8}))\)",
        Opts);

    // RPCS3 on Windows prints fmt::win_error_to_string as "<system text> (0x<code>)"
    // (Utilities/StrFmt.cpp:80), inside "(error=...)" after a failure. The decoded
    // code is an observation of what the OS reported, never a root cause.
    public static bool TryMatchWin32(string message, out int code, out string? text, out string raw)
    {
        code = 0;
        text = null;
        raw = string.Empty;
        var match = Win32.Match(message);
        if (!match.Success)
        {
            return false;
        }

        var hex = match.Groups["c1"].Success ? match.Groups["c1"].Value : match.Groups["c2"].Value;
        if (!int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code))
        {
            return false;
        }

        text = match.Groups["text"].Success && match.Groups["text"].Value.Length > 0 ? match.Groups["text"].Value.Trim() : null;
        raw = match.Value;
        return true;
    }
}

#pragma warning restore IDE0130
