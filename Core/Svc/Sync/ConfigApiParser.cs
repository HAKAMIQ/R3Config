using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using R3Config.Mdl;

namespace R3Config.Svc;

public sealed record ConfigApiIssue(
    string Serial,
    string Detail);

public sealed record ConfigApiSnapshot(
    long Timestamp,
    IReadOnlyList<RecSyncApiGame> Games,
    IReadOnlyList<ConfigApiIssue> Issues);

// Strict reader for https://api.rpcs3.net/config/?api=v1. The response is
// {"return_code":0,"timestamp":<unix>,"games":{"<SERIAL>":{"config":"<yaml>"}}}
// where each config is a small RPCS3 YAML fragment (sections, an optional
// nested map, scalar values and "- item" sequences). Anything outside that
// grammar is rejected for that game; a response that is mostly invalid or
// truncated is rejected as a whole.
public static partial class ConfigApiParser
{
    public const long MaxResponseBytes = 8L * 1024 * 1024;
    public const string LocalDatabaseFileName = "config_database.dat";
    public const int MinGames = 100;
    public const int MaxGames = 20_000;
    private const int MaxConfigChars = 4096;
    private const int MaxLines = 64;
    private const int MaxLineChars = 256;
    private const int MaxDepth = 2;

    private static readonly string[] Sections =
    [
        "Core",
        "VFS",
        "Video",
        "Audio",
        "Input/Output",
        "System",
        "Net",
        "Savestate",
        "Miscellaneous"
    ];

    public static ConfigApiSnapshot Parse(byte[] body)
    {
        ArgumentNullException.ThrowIfNull(body);

        if (body.LongLength == 0 || body.LongLength > MaxResponseBytes)
        {
            throw new InvalidDataException("Config API response is empty or exceeds the size limit.");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(
                body,
                new JsonDocumentOptions { MaxDepth = 8 });
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Config API response is not valid JSON.", ex);
        }

        using (document)
        {
            try
            {
                return Read(document.RootElement);
            }
            catch (InvalidOperationException ex)
            {
                // A lone surrogate escape is valid JSON syntax but not text.
                throw new InvalidDataException("Config API response contains text that is not valid UTF-16.", ex);
            }
        }
    }

    private static ConfigApiSnapshot Read(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Config API response root is not an object.");
        }

        RejectDuplicateKeys(root);

        if (!root.TryGetProperty("return_code", out var code) ||
            code.ValueKind != JsonValueKind.Number ||
            !code.TryGetInt32(out var returnCode) ||
            returnCode != 0)
        {
            throw new InvalidDataException("Config API returned a non-zero or missing return_code.");
        }

        if (!root.TryGetProperty("timestamp", out var stamp) ||
            stamp.ValueKind != JsonValueKind.Number ||
            !stamp.TryGetInt64(out var timestamp) ||
            timestamp <= 0)
        {
            throw new InvalidDataException("Config API timestamp is missing or invalid.");
        }

        if (!root.TryGetProperty("games", out var games) ||
            games.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Config API games object is missing.");
        }

        var parsed = new List<RecSyncApiGame>();
        var issues = new List<ConfigApiIssue>();
        var total = 0;

        foreach (var game in games.EnumerateObject())
        {
            total++;
            if (total > MaxGames)
            {
                throw new InvalidDataException("Config API lists more games than the input limit.");
            }

            var serial = game.Name;
            if (!SerialRule().IsMatch(serial))
            {
                issues.Add(new ConfigApiIssue(Safe(serial), "Invalid serial."));
                continue;
            }

            if (game.Value.ValueKind != JsonValueKind.Object ||
                !game.Value.TryGetProperty("config", out var config) ||
                config.ValueKind != JsonValueKind.String)
            {
                issues.Add(new ConfigApiIssue(serial, "Missing config text."));
                continue;
            }

            var text = config.GetString() ?? string.Empty;
            if (!TryParseConfig(text, out var settings, out var detail))
            {
                issues.Add(new ConfigApiIssue(serial, detail));
                continue;
            }

            parsed.Add(new RecSyncApiGame(serial, settings));
        }

        if (parsed.Count < MinGames)
        {
            throw new InvalidDataException(
                $"Config API response lists {parsed.Count} valid games; a complete response has at least {MinGames}.");
        }

        if (issues.Count * 20 > total)
        {
            throw new InvalidDataException(
                $"Config API response has {issues.Count} invalid games out of {total}.");
        }

        return new ConfigApiSnapshot(
            timestamp,
            [.. parsed.OrderBy(game => game.Serial, StringComparer.Ordinal)],
            issues);
    }

    // RPCS3 (rpcs3qt/config_database.cpp) writes the response it downloaded
    // and validated, unchanged, to <config dir>/GuiConfigs/config_database.dat,
    // where the config dir is the data root R3Config already resolves.
    public static string? LocalDatabasePath(Rpcs3InstallDescriptor? install) =>
        install is null
            ? null
            : Path.Combine(install.DataRoot ?? install.RootPath, "GuiConfigs", LocalDatabaseFileName);

    public static bool TryParseConfig(
        string text,
        out IReadOnlyList<RecSyncSetting> settings,
        out string detail)
    {
        settings = [];
        detail = string.Empty;

        if (text.Length == 0 || text.Length > MaxConfigChars)
        {
            detail = "Config text is empty or too long.";
            return false;
        }

        var lines = text.Split('\n');
        if (lines.Length > MaxLines)
        {
            detail = "Config text has too many lines.";
            return false;
        }

        var path = new List<string>();
        var values = new Dictionary<string, (string Section, string Key, string? Scalar, List<string>? Items)>(StringComparer.Ordinal);
        string? sequenceOwner = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            if (line.Trim().Length == 0)
            {
                continue;
            }

            if (line.Length > MaxLineChars ||
                line.Any(ch => char.IsControl(ch)) ||
                line.TrimEnd().Length != line.Length)
            {
                detail = "Config line is too long or contains control characters.";
                return false;
            }

            var indent = line.Length - line.TrimStart(' ').Length;
            if (indent % 2 != 0)
            {
                detail = "Config indentation is not a multiple of two spaces.";
                return false;
            }

            var depth = indent / 2;
            var body = line[indent..];

            if (body.StartsWith("- ", StringComparison.Ordinal))
            {
                if (sequenceOwner is null || depth != path.Count)
                {
                    detail = "Sequence item has no owning key.";
                    return false;
                }

                var item = body[2..];
                if (!IsScalar(item))
                {
                    detail = "Sequence item is not a plain scalar.";
                    return false;
                }

                values[sequenceOwner].Items!.Add(item);
                continue;
            }

            if (depth > path.Count || depth > MaxDepth)
            {
                detail = "Config nesting is deeper than supported.";
                return false;
            }

            path.RemoveRange(depth, path.Count - depth);
            sequenceOwner = null;

            var colon = body.IndexOf(':', StringComparison.Ordinal);
            if (colon <= 0)
            {
                detail = "Config line is not a key.";
                return false;
            }

            var key = body[..colon];
            var rest = body[(colon + 1)..];
            if (!KeyRule().IsMatch(key))
            {
                detail = "Config key contains unsupported characters.";
                return false;
            }

            if (depth == 0 && !Sections.Contains(key, StringComparer.Ordinal))
            {
                detail = $"Unknown config section '{key}'.";
                return false;
            }

            if (rest.Length == 0)
            {
                if (depth == 0)
                {
                    path.Add(key);
                    continue;
                }

                // A key without a value is either a nested map or a sequence;
                // the next line decides. Record it as a possible sequence.
                path.Add(key);
                var owner = string.Join('.', path.Skip(1));
                if (!values.TryAdd(Id(path[0], owner), (path[0], owner, null, [])))
                {
                    detail = "Duplicate config key.";
                    return false;
                }

                sequenceOwner = Id(path[0], owner);
                continue;
            }

            if (depth == 0 || !rest.StartsWith(' ') || !IsScalar(rest[1..]))
            {
                detail = "Config value is not a plain scalar.";
                return false;
            }

            var settingKey = string.Join('.', path.Skip(1).Append(key));
            if (!values.TryAdd(Id(path[0], settingKey), (path[0], settingKey, rest[1..], null)))
            {
                detail = "Duplicate config key.";
                return false;
            }
        }

        var output = new List<RecSyncSetting>();
        foreach (var value in values.Values)
        {
            if (value.Scalar is not null)
            {
                output.Add(new RecSyncSetting(value.Section, value.Key, value.Scalar));
                continue;
            }

            if (value.Items is { Count: > 0 })
            {
                output.Add(new RecSyncSetting(
                    value.Section,
                    value.Key,
                    string.Join(", ", value.Items.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))));
            }

            // An empty owner was a nested-map header; its children are listed.
        }

        if (output.Count == 0)
        {
            detail = "Config text has no settings.";
            return false;
        }

        settings = [.. output
            .OrderBy(setting => setting.Section, StringComparer.Ordinal)
            .ThenBy(setting => setting.Key, StringComparer.Ordinal)];
        return true;
    }

    private static string Id(string section, string key) => $"{section}\0{key}";

    private static bool IsScalar(string value)
    {
        return value.Length is > 0 and <= 128 &&
               value.Trim().Length == value.Length &&
               "{[&*!|>'\"%@`#".IndexOf(value[0], StringComparison.Ordinal) < 0 &&
               !value.Contains(": ", StringComparison.Ordinal) &&
               !value.Contains(" #", StringComparison.Ordinal);
    }

    private static string Safe(string value)
    {
        var builder = new StringBuilder();
        foreach (var ch in value.Take(16))
        {
            builder.Append(char.IsLetterOrDigit(ch) ? ch : '?');
        }

        return builder.ToString();
    }

    private static void RejectDuplicateKeys(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new InvalidDataException(
                        $"Config API response repeats the key '{Safe(property.Name)}'.");
                }

                RejectDuplicateKeys(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                RejectDuplicateKeys(item);
            }
        }
    }

    [GeneratedRegex(@"^[A-Z]{4}[0-9]{5}$", RegexOptions.CultureInvariant)]
    private static partial Regex SerialRule();

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9 /()&+.\-_]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyRule();
}
