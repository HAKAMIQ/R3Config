using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using R3Config.Mdl;

namespace R3Config.Svc;

public sealed partial class WikiSvc
{
    private const long MaxWikiXmlBytes = 128L * 1024 * 1024;

    private static readonly Regex Template = new(
        @"\{\{gameid\|(?<body>.*?)\}\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.IgnoreCase);

    private static readonly Regex Pattern = new(
        @"[A-Z]{4}\d{5}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

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
            "dump to file",
            "convert to 16-bit",
            "enable buffering",
            "enable time stretching"
        };

    private static readonly Dictionary<string, (string Section, string Key)> CfgMap =
        new Dictionary<string, (string Section, string Key)>(StringComparer.OrdinalIgnoreCase)
        {
            ["ppu decoder"] = ("Core", "PPU Decoder"),
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
            ["ppu llvm java mode handling"] = ("Core", "PPU LLVM Java Mode Handling"),
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
            ["psn status"] = ("Net", "PSN status")
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

        var file = new FileInfo(wikiXmlPath);
        if (file.Length < 0 || file.Length > MaxWikiXmlBytes)
        {
            throw new InvalidDataException(
                $"RPCS3 wiki XML exceeds the {MaxWikiXmlBytes}-byte input limit.");
        }

        using var stream = new FileStream(
            wikiXmlPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        using var reader = XmlReader.Create(
            stream,
            new XmlReaderSettings
            {
                CloseInput = false,
                DtdProcessing = DtdProcessing.Prohibit,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
                MaxCharactersFromEntities = 0,
                MaxCharactersInDocument = MaxWikiXmlBytes,
                XmlResolver = null
            });
        var document = XDocument.Load(
            reader,
            LoadOptions.None);
        var recommendations = new Dictionary<string, WikiRec>(StringComparer.OrdinalIgnoreCase);

        foreach (var page in document.Descendants().Where(element => element.Name.LocalName == "page"))
        {
            var title = page.Elements().FirstOrDefault(element => element.Name.LocalName == "title")?.Value;
            var text = page.Descendants().FirstOrDefault(element => element.Name.LocalName == "text")?.Value;

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var serials = Extract(text).ToArray();
            if (serials.Length == 0)
            {
                continue;
            }

            var changes = Extract2(text).ToArray();
            foreach (var serial in serials)
            {
                var candidate = new WikiRec(
                    serial,
                    title.Trim(),
                    changes);

                if (!recommendations.TryGetValue(
                        serial,
                        out var existing))
                {
                    recommendations.Add(serial, candidate);
                    continue;
                }

                if (!string.Equals(
                        existing.WikiName,
                        candidate.WikiName,
                        StringComparison.Ordinal) ||
                    !existing.Changes.SequenceEqual(candidate.Changes))
                {
                    throw new InvalidDataException(
                        $"RPCS3 wiki XML contains conflicting recommendations for serial '{serial}'.");
                }
            }
        }

        return new WikiSvc(recommendations);
    }

    private static IEnumerable<string> Extract(string text)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var matches = Template.Matches(text);

        foreach (Match match in matches)
        {
            foreach (Match serialMatch in Pattern.Matches(match.Groups["body"].Value))
            {
                if (seen.Add(serialMatch.Value))
                {
                    yield return serialMatch.Value;
                }
            }
        }

        if (seen.Count == 0)
        {
            foreach (Match serialMatch in Pattern.Matches(text))
            {
                if (seen.Add(serialMatch.Value))
                {
                    yield return serialMatch.Value;
                }
            }
        }
    }

    private static IEnumerable<CfgChg> Extract2(string text)
    {
        var block = Extract3(text);
        if (string.IsNullOrWhiteSpace(block))
        {
            yield break;
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var notes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in block.Split('\n'))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith('|'))
            {
                continue;
            }

            var equalsIndex = trimmed.IndexOf('=');
            if (equalsIndex < 0)
            {
                continue;
            }

            var rawKey = NormWiki(trimmed[1..equalsIndex]);
            var rawValue = CleanVal(trimmed[(equalsIndex + 1)..]);
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                continue;
            }

            if (rawKey.EndsWith(" notes", StringComparison.OrdinalIgnoreCase))
            {
                AddUnique(
                    notes,
                    rawKey[..^6].Trim(),
                    rawValue);
            }
            else
            {
                AddUnique(values, rawKey, rawValue);
            }
        }

        foreach (var (wikiKey, rawValue) in values.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            notes.TryGetValue(wikiKey, out var note);

            if (wikiKey.Equals("zcull accuracy", StringComparison.OrdinalIgnoreCase))
            {
                var zcull = ConvertZCull(rawValue);
                if (zcull is null)
                {
                    continue;
                }

                var risk = RecPol.Classify(wikiKey, rawValue, note);
                yield return new CfgChg(
                    Section: "Video",
                    Key: "Relaxed ZCULL Sync",
                    Value: zcull.Value.Relaxed,
                    WikiKey: wikiKey,
                    Notes: note,
                    Risk: risk);
                yield return new CfgChg(
                    Section: "Video",
                    Key: "Accurate ZCULL stats",
                    Value: zcull.Value.Precise,
                    WikiKey: wikiKey,
                    Notes: note,
                    Risk: risk);
                continue;
            }

            if (!CfgMap.TryGetValue(wikiKey, out var target))
            {
                continue;
            }

            var value = Convert(wikiKey, rawValue);
            if (value is null)
            {
                continue;
            }

            yield return new CfgChg(
                Section: target.Section,
                Key: target.Key,
                Value: value,
                WikiKey: wikiKey,
                Notes: note,
                Risk: RecPol.Classify(wikiKey, value, note));
        }
    }

    private static void AddUnique(
        Dictionary<string, string> values,
        string key,
        string value)
    {
        if (!values.TryGetValue(key, out var existing))
        {
            values.Add(key, value);
            return;
        }

        if (!string.Equals(
                existing,
                value,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"RPCS3 wiki XML contains conflicting values for config key '{key}'.");
        }
    }

    private static string? Extract3(string text)
    {
        var start = text.IndexOf("{{config", StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return null;
        }

        var end = text.IndexOf("\n}}", start, StringComparison.Ordinal);
        if (end < 0)
        {
            return null;
        }

        return text[start..end];
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
                   value.Equals("Ordered & Atomic", StringComparison.OrdinalIgnoreCase)
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
                   lodBias is >= -32m and <= 32m
                ? lodBias.ToString(CultureInfo.InvariantCulture)
                : null;
        }

        if (wikiKey.Equals("resolution scale threshold", StringComparison.OrdinalIgnoreCase))
        {
            if (TryParse(value, 1, 1024, out var directDimension))
            {
                return directDimension.ToString(CultureInfo.InvariantCulture);
            }

            var dimensionMatch = Dim.Match(value);
            if (!dimensionMatch.Success ||
                !int.TryParse(dimensionMatch.Groups["width"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var width) ||
                !int.TryParse(dimensionMatch.Groups["height"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var height) ||
                width != height ||
                width is < 1 or > 1024)
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

            return TryParse(numericValue, 0, 16, out var anisotropic)
                ? anisotropic.ToString(CultureInfo.InvariantCulture)
                : null;
        }

        if (wikiKey.Equals("preferred spu threads", StringComparison.OrdinalIgnoreCase))
        {
            if (Range.IsMatch(value))
            {
                return null;
            }

            return TryParse(value, 0, 6, out var preferredThreads)
                ? preferredThreads.ToString(CultureInfo.InvariantCulture)
                : null;
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

    private static bool TryParse(string value, int minimum, int maximum, out int result)
    {
        return int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out result) &&
               result >= minimum &&
               result <= maximum;
    }
}
