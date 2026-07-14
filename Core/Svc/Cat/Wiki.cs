using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using R3Config.Mdl;

namespace R3Config.Svc;

public sealed class WikiRecommendationService
{
    private static readonly Regex GameIdTemplate = new(
        @"\{\{gameid\|(?<body>.*?)\}\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.IgnoreCase);

    private static readonly Regex SerialPattern = new(
        @"[A-Z]{4}\d{5}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex HtmlComment = new(
        @"<!--.*?-->",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);

    private static readonly Regex HtmlBreak = new(
        @"<br\s*/?>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex WikiLink = new(
        @"\[\[(?<body>.*?)\]\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);

    private static readonly Regex WikiIssue = new(
        @"\{\{issue\|(?<id>\d+)\}\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex WikiPullRequest = new(
        @"\{\{pr\|(?<id>\d+)\}\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex SquareDimension = new(
        @"^(?<width>\d+)\s*[x×]\s*(?<height>\d+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex IntegerRange = new(
        @"^\d+\s*[-–—]\s*\d+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex AnisotropicValue = new(
        @"^(?<value>\d+)\s*x$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly IReadOnlySet<string> BooleanConfigKeys =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "enable spu loop detection",
            "accurate spu dma",
            "accurate rsx reservation access",
            "accurate llvm dfma",
            "ppu llvm java mode handling",
            "ppu llvm accurate vector nans",
            "debug console mode",
            "hook static functions",
            "write color buffers",
            "read color buffers",
            "write depth buffers",
            "read depth buffers",
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

    private static readonly IReadOnlyDictionary<string, (string Section, string Key)> ConfigMap =
        new Dictionary<string, (string Section, string Key)>(StringComparer.OrdinalIgnoreCase)
        {
            ["ppu decoder"] = ("Core", "PPU Decoder"),
            ["spu decoder"] = ("Core", "SPU Decoder"),
            ["preferred spu threads"] = ("Core", "Preferred SPU Threads"),
            ["thread scheduler"] = ("Core", "Thread Scheduler Mode"),
            ["enable spu loop detection"] = ("Core", "SPU loop detection"),
            ["spu xfloat accuracy"] = ("Core", "SPU XFloat Accuracy"),
            ["spu block size"] = ("Core", "SPU Block Size"),
            ["accurate spu dma"] = ("Core", "Accurate SPU DMA"),
            ["accurate rsx reservation access"] = ("Core", "Accurate RSX reservation access"),
            ["maximum spurs threads"] = ("Core", "Max SPURS Threads"),
            ["sleep timers accuracy"] = ("Core", "Sleep Timers Accuracy"),
            ["clocks scale"] = ("Core", "Clocks scale"),
            ["accurate llvm dfma"] = ("Core", "Use Accurate DFMA"),
            ["ppu llvm java mode handling"] = ("Core", "PPU LLVM Java Mode Handling"),
            ["ppu llvm accurate vector nans"] = ("Core", "PPU Accurate Vector NaN Values"),
            ["debug console mode"] = ("Core", "Debug Console Mode"),
            ["hook static functions"] = ("Core", "Hook static functions"),

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

    private readonly IReadOnlyDictionary<string, WikiGameRecommendation> _recommendations;

    private WikiRecommendationService(IReadOnlyDictionary<string, WikiGameRecommendation> recommendations)
    {
        _recommendations = recommendations;
    }

    public int Count => _recommendations.Count;

    public IReadOnlyCollection<WikiGameRecommendation> Recommendations => _recommendations.Values.ToArray();

    public WikiGameRecommendation? Find(string serial)
    {
        return _recommendations.TryGetValue(serial, out var recommendation)
            ? recommendation
            : null;
    }

    public static WikiRecommendationService Load(string wikiXmlPath)
    {
        if (!File.Exists(wikiXmlPath))
        {
            return new WikiRecommendationService(new Dictionary<string, WikiGameRecommendation>());
        }

        var document = XDocument.Load(wikiXmlPath, LoadOptions.None);
        var recommendations = new Dictionary<string, WikiGameRecommendation>(StringComparer.OrdinalIgnoreCase);

        foreach (var page in document.Descendants().Where(element => element.Name.LocalName == "page"))
        {
            var title = page.Elements().FirstOrDefault(element => element.Name.LocalName == "title")?.Value;
            var text = page.Descendants().FirstOrDefault(element => element.Name.LocalName == "text")?.Value;

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var serials = ExtractSerials(text).ToArray();
            if (serials.Length == 0)
            {
                continue;
            }

            var changes = ExtractConfigChanges(text).ToArray();
            foreach (var serial in serials)
            {
                recommendations[serial] = new WikiGameRecommendation(serial, title.Trim(), changes);
            }
        }

        return new WikiRecommendationService(recommendations);
    }

    private static IEnumerable<string> ExtractSerials(string text)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var matches = GameIdTemplate.Matches(text);

        foreach (Match match in matches)
        {
            foreach (Match serialMatch in SerialPattern.Matches(match.Groups["body"].Value))
            {
                if (seen.Add(serialMatch.Value))
                {
                    yield return serialMatch.Value;
                }
            }
        }

        if (seen.Count == 0)
        {
            foreach (Match serialMatch in SerialPattern.Matches(text))
            {
                if (seen.Add(serialMatch.Value))
                {
                    yield return serialMatch.Value;
                }
            }
        }
    }

    private static IEnumerable<ConfigChange> ExtractConfigChanges(string text)
    {
        var block = ExtractConfigBlock(text);
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

            var rawKey = NormalizeWikiKey(trimmed[1..equalsIndex]);
            var rawValue = CleanWikiValue(trimmed[(equalsIndex + 1)..]);
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                continue;
            }

            if (rawKey.EndsWith(" notes", StringComparison.OrdinalIgnoreCase))
            {
                notes[rawKey[..^6].Trim()] = rawValue;
            }
            else
            {
                values[rawKey] = rawValue;
            }
        }

        foreach (var (wikiKey, rawValue) in values.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (!ConfigMap.TryGetValue(wikiKey, out var target))
            {
                continue;
            }

            var value = ConvertWikiValue(wikiKey, rawValue);
            if (value is null)
            {
                continue;
            }

            notes.TryGetValue(wikiKey, out var note);

            yield return new ConfigChange(
                Section: target.Section,
                Key: target.Key,
                Value: value,
                WikiKey: wikiKey,
                Notes: note,
                Risk: RecommendationRiskPolicy.Classify(wikiKey, value, note));
        }
    }

    private static string? ExtractConfigBlock(string text)
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

    private static string NormalizeWikiKey(string key)
    {
        return Regex.Replace(key, @"\s+", " ").Trim().ToLowerInvariant();
    }

    private static string CleanWikiValue(string value)
    {
        value = HtmlComment.Replace(value, string.Empty);
        value = HtmlBreak.Replace(value, " ");
        value = WikiIssue.Replace(value, "RPCS3 issue #${id}");
        value = WikiPullRequest.Replace(value, "RPCS3 PR #${id}");
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

    private static string? ConvertWikiValue(string wikiKey, string value)
    {
        if (wikiKey.Equals("resolution scale threshold", StringComparison.OrdinalIgnoreCase))
        {
            if (TryParseBoundedInteger(value, 1, 1024, out var directDimension))
            {
                return directDimension.ToString(CultureInfo.InvariantCulture);
            }

            var dimensionMatch = SquareDimension.Match(value);
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

            var anisotropicMatch = AnisotropicValue.Match(value);
            var numericValue = anisotropicMatch.Success
                ? anisotropicMatch.Groups["value"].Value
                : value;

            return TryParseBoundedInteger(numericValue, 0, 16, out var anisotropic)
                ? anisotropic.ToString(CultureInfo.InvariantCulture)
                : null;
        }

        if (wikiKey.Equals("preferred spu threads", StringComparison.OrdinalIgnoreCase))
        {
            if (IntegerRange.IsMatch(value))
            {
                return null;
            }

            return TryParseBoundedInteger(value, 0, 6, out var preferredThreads)
                ? preferredThreads.ToString(CultureInfo.InvariantCulture)
                : null;
        }

        if (BooleanConfigKeys.Contains(wikiKey))
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

    private static bool TryParseBoundedInteger(string value, int minimum, int maximum, out int result)
    {
        return int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out result) &&
               result >= minimum &&
               result <= maximum;
    }
}
