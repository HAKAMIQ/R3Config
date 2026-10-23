using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using R3Config.Mdl;

#pragma warning disable IDE0130

namespace R3Config.Svc;

// The explicit, table-driven rules that turn one RPCS3 log line into
// diagnostic categories, and the deterministic signature of a problem event.
// A rule fires only on the event's own level, channel and text (the patterns
// were taken from real RPCS3 logs); nothing is inferred from the game, the
// hardware or the surrounding lines. No rule for a category means the category
// is never emitted.
public static class LogRules
{
    private const RegexOptions Opts =
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase;

    private sealed record Rule(string Levels, Regex? Channel, Regex? Message, LogCategory[] Categories);

    private static Rule R(string levels, string? channel, string? message, params LogCategory[] categories) =>
        new(
            levels,
            channel is null ? null : new Regex(channel, Opts),
            message is null ? null : new Regex(message, Opts),
            categories);

    // Ordered: every matching rule contributes, first contribution is primary.
    private static readonly Rule[] Rules =
    [
        // Lifecycle (any level).
        R("*", "^SYS$", @"^Emulator::BootGame", LogCategory.GameBoot),
        R("*", "^SYS$", @"^Quit with |^RPCS3 terminated with exit code", LogCategory.GameExit),
        R("*", "^sys_process$", @"^Process finished", LogCategory.GameExit),

        // Problems whose own text names the failure (errors and fatals only).
        R("EF", null, @"access violation|segmentation fault|\bsegfault\b|unhandled exception|stack overflow|\bSIGSEGV\b", LogCategory.Crash),
        R("F", "^SIG$", @"^Thread terminated due to fatal error", LogCategory.FatalError),
        R("EF", null, @"\bVerification failed\b|\bassertion\b.*\bfailed\b|^Assert", LogCategory.Assertion),
        R("EF", null, @"Failed to compile (?:fragment|vertex|compute|geometry) shader|shader (?:compilation|compile) (?:failed|error)", LogCategory.ShaderCompilationEvent, LogCategory.RenderingError),
        R("EF", null, @"out of (?:host |device )?memory|bad_alloc|VK_ERROR_OUT_OF_(?:HOST|DEVICE)_MEMORY", LogCategory.OutOfMemory, LogCategory.MemoryError),
        R("EF", null, @"\bVK_ERROR_[A-Z_]+|\bvk[A-Z]\w+\(?.*\bfailed\b|Vulkan.*\b(?:error|failed)\b", LogCategory.VulkanError, LogCategory.RenderingError),
        R("EF", null, @"OpenGL.*\b(?:error|failed)\b|\bGL_[A-Z_]*ERROR\b|glGetError", LogCategory.OpenglError, LogCategory.RenderingError),
        R("EF", "^SYS$", @"Virtual File System", LogCategory.VfsError),
        R("F", null, null, LogCategory.FatalError),

        // Subsystem errors (errors and fatals only; warnings there are HLE traces).
        R("EF", "^RSX$", @"timed out", LogCategory.Timeout, LogCategory.FreezeOrStallIndicator, LogCategory.RsxError),
        R("EF", "^RSX$", null, LogCategory.RsxError),
        R("EF", "^(?:PPU|PPU TODO)$", null, LogCategory.PpuError),
        R("EF", "^(?:SPU|SPU TODO)$", null, LogCategory.SpuError),
        R("EF", "^VFS$", null, LogCategory.VfsError),
        R("EF", "^sys_fs\\w*$", null, LogCategory.FilesystemError),
        R("EF", "^(?:cellSaveData|cellGame|cellGameExec|cellGameData)\\w*$", null, LogCategory.GameDataError),
        R("EF", "^(?:cellAudio\\w*|Cubeb|XAudio\\w*|FAudio|OpenAL|Audio)$", null, LogCategory.AudioError),
        R("EF", "^(?:ppu_loader|sys_prx|LDR|cellSysmodule)$", @"\.sprx|\bPRX\b|\bmodule\b", LogCategory.PrxError, LogCategory.ModuleLoadError),
        R("EF", "^(?:ppu_loader|LDR)$", null, LogCategory.ModuleLoadError),
        R("EF", "^(?:sys_memory|sys_mmapper|VM)$", null, LogCategory.MemoryError),
        R("EF", "^rpcn\\w*$", null, LogCategory.RpcnError),
        R("EF", "^(?:sys_net\\w*|cellNetCtl|UPNP|NET|sceNp\\w*)$", null, LogCategory.NetworkError),

        // HLE calls that returned an error code, classified by the function's
        // own name (the call name is the evidence, not the surrounding lines).
        R("E", null, @"^(?:Stat: )?'_?sys_fs\w*' failed", LogCategory.FilesystemError),
        R("E", null, @"^(?:Stat: )?'_?sys_prx\w*' failed|\bCELL_PRX_ERROR", LogCategory.PrxError, LogCategory.ModuleLoadError),
        R("E", null, @"^(?:Stat: )?'(?:cellNetCtl|sceNp|sys_net)\w*' failed", LogCategory.NetworkError),
        R("E", null, @"^(?:Stat: )?'(?:cellGame|cellSaveData)\w*' failed", LogCategory.GameDataError),
        R("E", null, @"^(?:Stat: )?'cellAudio\w*' failed", LogCategory.AudioError),
        R("E", null, @"^(?:Stat: )?'(?:sys_memory|sys_mmapper)\w*' failed", LogCategory.MemoryError),

        // Documented performance warning (RSX shader cache miss).
        R("W", "^RSX$", @"^Cache miss at address .* This is gonna hurt", LogCategory.PerformanceWarning),
        R("W", null, @"\btimed out\b", LogCategory.Timeout),

        // Shader compilation activity (informational, counted).
        R("S!", "^RSX$", @"^Program compiled successfully", LogCategory.ShaderCompilationEvent)
    ];

    // Stable identity of the classification rule table (part of the analysis
    // manifest): changes whenever a level, channel, message pattern or category changes.
    public static string RuleSetVersion { get; } = ComputeRuleSetVersion();

    private static string ComputeRuleSetVersion()
    {
        var builder = new StringBuilder();
        foreach (var rule in Rules)
        {
            builder.Append(rule.Levels).Append('|').Append(rule.Channel).Append('|').Append(rule.Message).Append('|')
                .Append(string.Join(',', rule.Categories)).Append('\n');
        }

        var rules = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())))[..12].ToLowerInvariant();
        return "c-" + rules + "+o-" + LogObservationRules.Fingerprint;
    }

    // Categories for one line, or empty when no rule matches. An error or fatal
    // that no specific rule explains is UnknownError, never a guessed cause.
    public static IReadOnlyList<LogCategory> Classify(char level, string channel, string message)
    {
        // RPCS3 logs this while stopping a thread that is slow to exit; it is
        // not a failure of its own.
        if (channel == "SIG" && message.Contains("is too sleepy", StringComparison.Ordinal))
        {
            return [];
        }

        var found = new List<LogCategory>();
        foreach (var rule in Rules)
        {
            if (rule.Levels != "*" && !rule.Levels.Contains(level, StringComparison.Ordinal))
            {
                continue;
            }

            if (rule.Channel is not null && !rule.Channel.IsMatch(channel))
            {
                continue;
            }

            if (rule.Message is not null && !rule.Message.IsMatch(message))
            {
                continue;
            }

            foreach (var category in rule.Categories)
            {
                if (!found.Contains(category))
                {
                    found.Add(category);
                }
            }
        }

        if (found.Count == 0 && level is 'E' or 'F')
        {
            found.Add(LogCategory.UnknownError);
        }

        return found;
    }

    // ---------------------------------------------------------- signatures

    private static readonly Regex Hash40 = new(@"PPU-[0-9a-f]{40}", Opts);
    private static readonly Regex Hex = new(@"0x[0-9a-f]+", Opts);
    private static readonly Regex WinPath = new(@"[A-Za-z]:[\\/][^\s'""]*", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex PsPath = new(@"(?<![\w])/(?:dev_\w+|app_home|games)[^\s'""]*", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex QuotedPath = new(@"'[^']*[\\/][^']*'", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Ipv4 = new(@"(?<![\w.])\d{1,3}(?:\.\d{1,3}){3}(?![\w.])", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Number = new(@"(?<![\w.])\d+(?:\.\d+)?(?![\w])", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Space = new(@"\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Code = new(@"\b(?:CELL_[A-Z0-9_]+|VK_[A-Z0-9_]+|SCE_[A-Z0-9_]+)\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Removes what varies between machines and runs (addresses, hashes, paths,
    // counters) and keeps the wording that identifies the failure.
    public static string NormalizeMessage(string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var text = Hash40.Replace(message, "PPU-#");
        text = QuotedPath.Replace(text, "'<path>'");
        text = WinPath.Replace(text, "<path>");
        text = PsPath.Replace(text, "<path>");
        text = Hex.Replace(text, "0x#");
        text = Ipv4.Replace(text, "<ip>");
        text = Number.Replace(text, "#");
        text = Space.Replace(text, " ").Trim();
        return text.Length > 200 ? text[..200] : text;
    }

    public static string? ErrorCodeOf(string message)
    {
        var match = Code.Match(message);
        return match.Success ? match.Value : null;
    }

    public static LogSignature BuildSignature(char level, string channel, string message)
    {
        var subsystem = channel.Trim();
        var pattern = NormalizeMessage(message);
        var code = ErrorCodeOf(message);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(
            string.Create(CultureInfo.InvariantCulture, $"1|{level}|{subsystem}|{pattern}|{code}")));
        return new LogSignature(
            "s1:" + Convert.ToHexString(bytes)[..12].ToLowerInvariant(),
            subsystem,
            level,
            pattern,
            code);
    }
}

#pragma warning restore IDE0130
