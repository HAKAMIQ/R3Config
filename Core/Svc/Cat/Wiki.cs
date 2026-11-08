using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using R3Config.Mdl;

namespace R3Config.Svc;

public sealed partial class WikiSvc
{
    public const string CanonicalSnapshotFileName =
        "RPCS3+Wiki-20260920160231.xml";
    public const string CanonicalSnapshotDate = "2026-09-20";
    public const string CanonicalSnapshotSha256 =
        "7977CA26176E12DD167241CDE77D68E6CC64CE24CB39C2682FD5E686CA7936C5";

    private static readonly Regex Comment = new(
        @"<!--.*?-->",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);

    private static readonly Regex Break = new(
        @"<br\s*/?>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex WikiLink = new(
        @"\[\[(?<body>.*?)\]\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);

    private static readonly Regex Issue = new(
        @"\{\{issue\|(?<id>\d+)\}\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex Request = new(
        @"\{\{pr\|(?<id>\d+)\}\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex Dim = new(
        @"^(?<width>\d+)\s*[x×]\s*(?<height>\d+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex Range = new(
        @"^\d+\s*[-–—]\s*\d+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex AnisoVal = new(
        @"^(?<value>\d+)\s*x$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly HashSet<string> Keys =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "enable spu loop detection",
            "accurate spu dma",
            "accurate rsx reservation access",
            "accurate llvm dfma",
            "accurate dfma",
            "disable spu getllar spin optimization",
            "ppu llvm java mode handling",
            "ppu llvm accurate vector nans",
            "debug console mode",
            "hook static functions",
            "silence all logs",
            "write color buffers",
            "read color buffers",
            "write depth buffers",
            "read depth buffers",
            "handle rsx memory tiling",
            "strict rendering mode",
            "stretch to display area",
            "multithreaded rsx",
            "asynchronous texture streaming",
            "disable on-disk shader cache",
            "disable vertex cache",
            "vblank ntsc fixup",
            "force cpu blit emulation",
            "force gpu texture scaling",
            "disable zcull occlusion queries",
            "allow host gpu labels",
            "force hardware msaa resolve",
            "emulate special depth comparison",
            "disable asynchronous memory manager",
            "dump to file",
            "convert to 16-bit",
            "enable buffering",
            "enable time stretching"
        };

    private static readonly HashSet<string> NullHandlerKeys =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "renderer",
            "audio out windows",
            "microphone type"
        };

    private static readonly Dictionary<string, (string Section, string Key)> CfgMap =
        new Dictionary<string, (string Section, string Key)>(StringComparer.OrdinalIgnoreCase)
        {
            ["ppu decoder"] = ("Core", "PPU Decoder"),
            ["ppu thread count"] = ("Core", "PPU Threads"),
            ["spu decoder"] = ("Core", "SPU Decoder"),
            ["preferred spu threads"] = ("Core", "Preferred SPU Threads"),
            ["thread scheduler"] = ("Core", "Thread Scheduler Mode"),
            ["enable spu loop detection"] = ("Core", "SPU loop detection"),
            ["spu xfloat accuracy"] = ("Core", "SPU XFloat Accuracy"),
            ["xfloat accuracy"] = ("Core", "SPU XFloat Accuracy"),
            ["spu block size"] = ("Core", "SPU Block Size"),
            ["accurate spu dma"] = ("Core", "Accurate SPU DMA"),
            ["accurate rsx reservation access"] = ("Core", "Accurate RSX reservation access"),
            ["disable spu getllar spin optimization"] = ("Core", "Disable SPU GETLLAR Spin Optimization"),
            ["rsx fifo accuracy"] = ("Core", "RSX FIFO Fetch Accuracy"),
            ["accurate ppu 128 reservations"] = ("Core", "Accurate PPU 128-byte Reservation Op Max Length"),
            ["maximum spurs threads"] = ("Core", "Max SPURS Threads"),
            ["sleep timers accuracy"] = ("Core", "Sleep Timers Accuracy"),
            ["clocks scale"] = ("Core", "Clocks scale"),
            ["accurate llvm dfma"] = ("Core", "Use Accurate DFMA"),
            ["accurate dfma"] = ("Core", "Use Accurate DFMA"),
            ["ppu llvm java mode handling"] = ("Core", "PPU Accurate Non-Java Mode"),
            ["ppu llvm accurate vector nans"] = ("Core", "PPU Accurate Vector NaN Values"),
            ["debug console mode"] = ("Core", "Debug Console Mode"),
            ["hook static functions"] = ("Core", "Hook static functions"),
            ["silence all logs"] = ("Miscellaneous", "Silence All Logs"),

            ["renderer"] = ("Video", "Renderer"),
            ["aspect ratio"] = ("Video", "Aspect ratio"),
            ["framelimit"] = ("Video", "Frame limit"),
            ["anisotropic filter"] = ("Video", "Anisotropic Filter Override"),
            ["anti-aliasing"] = ("Video", "MSAA"),
            ["shader quality"] = ("Video", "Shader Precision"),
            ["3d rendering mode"] = ("Video", "3D Display Mode"),
            ["default resolution"] = ("Video", "Resolution"),
            ["resolution scale"] = ("Video", "Resolution Scale"),
            ["resolution scale threshold"] = ("Video", "Minimum Scalable Dimension"),
            ["shader mode"] = ("Video", "Shader Mode"),
            ["write color buffers"] = ("Video", "Write Color Buffers"),
            ["read color buffers"] = ("Video", "Read Color Buffers"),
            ["write depth buffers"] = ("Video", "Write Depth Buffer"),
            ["read depth buffers"] = ("Video", "Read Depth Buffer"),
            ["handle rsx memory tiling"] = ("Video", "Handle RSX Memory Tiling"),
            ["strict rendering mode"] = ("Video", "Strict Rendering Mode"),
            ["vsync"] = ("Video", "VSync Mode"),
            ["stretch to display area"] = ("Video", "Stretch To Display Area"),
            ["multithreaded rsx"] = ("Video", "Multithreaded RSX"),
            ["asynchronous texture streaming"] = ("Video", "Vulkan.Asynchronous Texture Streaming"),
            ["disable on-disk shader cache"] = ("Video", "Disable On-Disk Shader Cache"),
            ["disable vertex cache"] = ("Video", "Disable Vertex Cache"),
            ["driver wake-up delay"] = ("Video", "Driver Wake-Up Delay"),
            ["vblank rate"] = ("Video", "Vblank Rate"),
            ["vblank ntsc fixup"] = ("Video", "Vblank NTSC Fixup"),
            ["force cpu blit emulation"] = ("Video", "Force CPU Blit"),
            ["force gpu texture scaling"] = ("Video", "Use GPU texture scaling"),
            ["disable zcull occlusion queries"] = ("Video", "Disable ZCull Occlusion Queries"),
            ["lod bias offset"] = ("Video", "Texture LOD Bias Addend"),
            ["output scaling"] = ("Video", "Output Scaling Mode"),
            ["allow host gpu labels"] = ("Video", "Allow Host GPU Labels"),
            ["force hardware msaa resolve"] = ("Video", "Force Hardware MSAA Resolve"),
            ["emulate special depth comparison"] = ("Video", "Emulate Special Depth Comparison"),
            ["disable asynchronous memory manager"] = ("Video", "Disable Asynchronous Memory Manager"),

            ["audio out windows"] = ("Audio", "Renderer"),
            ["audio format"] = ("Audio", "Audio Format"),
            ["dump to file"] = ("Audio", "Dump to file"),
            ["convert to 16-bit"] = ("Audio", "Convert to 16 bit"),
            ["volume"] = ("Audio", "Master Volume"),
            ["enable buffering"] = ("Audio", "Enable Buffering"),
            ["audio buffer duration"] = ("Audio", "Desired Audio Buffer Duration"),
            ["enable time stretching"] = ("Audio", "Enable Time Stretching"),
            ["time stretching threshold"] = ("Audio", "Time Stretching Threshold"),
            ["microphone type"] = ("Audio", "Microphone Type"),

            ["keyboard handler"] = ("Input/Output", "Keyboard"),
            ["mouse handler"] = ("Input/Output", "Mouse"),
            ["camera input"] = ("Input/Output", "Camera type"),
            ["camera settings"] = ("Input/Output", "Camera"),
            ["move handler"] = ("Input/Output", "Move"),

            ["network status"] = ("Net", "Internet enabled"),
            ["psn status"] = ("Net", "PSN status"),

            // RPCS3 stores the LLE library selection as the sequence
            // "Core: Libraries Control" of "<name>.sprx:lle" entries; the
            // Wiki itself writes the entries in that form ("libvdec.sprx:lle").
            ["firmware libraries"] = ("Core", "Libraries Control")
        };

    private readonly IReadOnlyDictionary<string, WikiRec> _Rec;

    private WikiSvc(IReadOnlyDictionary<string, WikiRec> recommendations)
    {
        _Rec = recommendations;
    }

    public int Count => _Rec.Count;

    public IReadOnlyCollection<WikiRec> Rec => _Rec.Values.ToArray();

    public WikiRec? Find(string serial)
    {
        return _Rec.TryGetValue(serial, out var recommendation)
            ? recommendation
            : null;
    }

    public static WikiSvc Load(string wikiXmlPath)
    {
        if (!File.Exists(wikiXmlPath))
        {
            return new WikiSvc(new Dictionary<string, WikiRec>());
        }

        var analysis = Analyze(wikiXmlPath, "1970-01-01");
        var conflict = analysis.Findings.FirstOrDefault(finding =>
            finding.Code is WikiFindingCode.DuplicateSerialConflict or
                WikiFindingCode.DuplicateSettingConflict);
        if (conflict is not null)
        {
            throw new InvalidDataException(
                $"RPCS3 wiki XML contains conflicting data: {conflict.Detail}");
        }

        return new WikiSvc(analysis.CatalogRecommendations.ToDictionary(
            recommendation => recommendation.Serial,
            StringComparer.OrdinalIgnoreCase));
    }

    private static string NormWiki(string key)
    {
        return Regex.Replace(key, @"\s+", " ").Trim().ToLowerInvariant();
    }

    private static string CleanVal(string value)
    {
        value = Comment.Replace(value, string.Empty);
        value = Break.Replace(value, " ");
        value = Issue.Replace(value, "RPCS3 issue #${id}");
        value = Request.Replace(value, "RPCS3 PR #${id}");
        value = WikiLink.Replace(value, match =>
        {
            var body = match.Groups["body"].Value;
            var separator = body.LastIndexOf('|');
            return separator >= 0
                ? body[(separator + 1)..].Trim()
                : body.Replace('_', ' ').Trim();
        });
        value = WebUtility.HtmlDecode(value);
        value = value.Replace("'''", string.Empty).Replace("''", string.Empty);
        return Regex.Replace(value, @"\s+", " ").Trim();
    }

    private static string? Convert(string wikiKey, string value)
    {
        if (wikiKey.Equals("accurate ppu 128 reservations", StringComparison.OrdinalIgnoreCase))
        {
            if (value.Equals("Always Enabled", StringComparison.OrdinalIgnoreCase))
            {
                return "-1";
            }

            return value.Equals("Disabled", StringComparison.OrdinalIgnoreCase)
                ? "0"
                : null;
        }

        if (wikiKey.Equals("rsx fifo accuracy", StringComparison.OrdinalIgnoreCase))
        {
            return value.Equals("Fast", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("Atomic", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("Ordered & Atomic", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("PS3", StringComparison.OrdinalIgnoreCase)
                ? value
                : null;
        }

        if (wikiKey.Equals("lod bias offset", StringComparison.OrdinalIgnoreCase))
        {
            return decimal.TryParse(
                       value,
                       NumberStyles.Float,
                       CultureInfo.InvariantCulture,
                       out var lodBias) &&
                   lodBias >= NumericBounds["lod bias offset"].Min &&
                   lodBias <= NumericBounds["lod bias offset"].Max
                ? lodBias.ToString(CultureInfo.InvariantCulture)
                : null;
        }

        if (wikiKey.Equals("resolution scale threshold", StringComparison.OrdinalIgnoreCase))
        {
            if (TryParse(value, "resolution scale threshold", out var directDimension))
            {
                return directDimension.ToString(CultureInfo.InvariantCulture);
            }

            var dimensionMatch = Dim.Match(value);
            if (!dimensionMatch.Success ||
                !int.TryParse(dimensionMatch.Groups["width"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var width) ||
                !int.TryParse(dimensionMatch.Groups["height"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var height) ||
                width != height ||
                !InBounds("resolution scale threshold", width))
            {
                return null;
            }

            return width.ToString(CultureInfo.InvariantCulture);
        }

        if (wikiKey.Equals("anisotropic filter", StringComparison.OrdinalIgnoreCase))
        {
            if (value.Equals("Auto", StringComparison.OrdinalIgnoreCase))
            {
                return "0";
            }

            var anisotropicMatch = AnisoVal.Match(value);
            var numericValue = anisotropicMatch.Success
                ? anisotropicMatch.Groups["value"].Value
                : value;

            return TryParse(numericValue, "anisotropic filter", out var anisotropic)
                ? anisotropic.ToString(CultureInfo.InvariantCulture)
                : null;
        }

        if (wikiKey.Equals("preferred spu threads", StringComparison.OrdinalIgnoreCase))
        {
            if (Range.IsMatch(value))
            {
                return null;
            }

            return TryParse(value, "preferred spu threads", out var preferredThreads)
                ? preferredThreads.ToString(CultureInfo.InvariantCulture)
                : null;
        }

        if (NullHandlerKeys.Contains(wikiKey) &&
            value.Equals("Disabled", StringComparison.OrdinalIgnoreCase))
        {
            return "Null";
        }

        if (Keys.Contains(wikiKey))
        {
            if (value.Equals("On", StringComparison.OrdinalIgnoreCase))
            {
                return "true";
            }

            if (value.Equals("Off", StringComparison.OrdinalIgnoreCase))
            {
                return "false";
            }
        }

        if (value.EndsWith('%') &&
            int.TryParse(value[..^1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var percent))
        {
            return percent.ToString(CultureInfo.InvariantCulture);
        }

        return value;
    }

    private static (string Relaxed, string Precise)? ConvertZCull(string value)
    {
        if (value.Equals("Precise", StringComparison.OrdinalIgnoreCase))
        {
            return ("false", "true");
        }

        if (value.Equals("Approximate", StringComparison.OrdinalIgnoreCase))
        {
            return ("false", "false");
        }

        return value.Equals("Relaxed", StringComparison.OrdinalIgnoreCase)
            ? ("true", "false")
            : null;
    }

    private static bool TryParse(string value, string wikiKey, out int result)
    {
        return int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out result) &&
               InBounds(wikiKey, result);
    }

    private static bool InBounds(string wikiKey, decimal value)
    {
        var bound = NumericBounds[wikiKey];
        return value >= bound.Min && value <= bound.Max;
    }
}
