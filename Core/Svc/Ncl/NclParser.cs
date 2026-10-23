using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using R3Config.Mdl;

namespace R3Config.Svc;

public static class NclParser
{
    private const long MaxNclBytes = 2L * 1024 * 1024;

    private static readonly Regex SerialRx = new(
        @"(?<![A-Z0-9])(?<v>[A-Z]{4}[0-9]{5})(?![A-Z0-9])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    // Version widths follow PatVer, the project's own version grammar, which
    // accepts one or two digits on each side. Requiring two here silently
    // demoted real evidence such as "BLUS31162 1.00" to unknown text and cost
    // the whole file its binding.
    private static readonly Regex IdentityTokenRx = new(
        @"(?<serial>(?<![A-Z0-9])[A-Z]{4}[0-9]{5}(?![A-Z0-9]))|(?<app>(?<![A-Z0-9])av(?<appv>[0-9]{1,2}\.[0-9]{1,2})(?![0-9]))|(?<disc>(?<![A-Z0-9])v(?<discv>[0-9]{1,2}\.[0-9]{1,2})(?![0-9]))|(?<bare>(?<![A-Z0-9])(?<barev>[0-9]{1,2}\.[0-9]{1,2})(?![A-Z0-9]))|(?<unknown>[A-Z0-9]+(?:\.[A-Z0-9]+)?)",
        RegexOptions.Compiled |
        RegexOptions.CultureInvariant |
        RegexOptions.IgnoreCase);

    private static readonly Regex ArtemisOptionRx = new(
        @"\[(?<id>Z+)\](?<body>.*?)\[/Z+\]",
        RegexOptions.Compiled |
        RegexOptions.CultureInvariant |
        RegexOptions.IgnoreCase |
        RegexOptions.Singleline);

    public static NclDoc Read(
        string path,
        NclDialect dialect = NclDialect.NetCheat453,
        NclOptionSelection? optionSelection = null)
    {
        var text = R3Bound.ReadUtf8(path, MaxNclBytes, "NCL file");
        return ReadText(text, path, dialect, optionSelection);
    }

    // optionSelection resolves the Artemis option placeholders of exactly one
    // cheat. Without it every option cheat keeps its unresolved state, so the
    // default behaviour of this parser is unchanged.
    public static NclDoc ReadText(
        string text,
        string? sourcePath = null,
        NclDialect dialect = NclDialect.NetCheat453,
        NclOptionSelection? optionSelection = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        R3Bound.Check(text, MaxNclBytes, "NCL text");

        var normalized = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .TrimStart('\uFEFF');

        if (normalized.Contains('#'))
        {
            return dialect == NclDialect.ArtemisPs3
                ? ParseModernArtemis(normalized, sourcePath, optionSelection)
                : ParseModernNetCheat(normalized, sourcePath);
        }

        return ParseLegacy(normalized, sourcePath, dialect, optionSelection);
    }

    public static NclSourceIdentity ParseIdentity(string path)
    {
        var fileName = Path.GetFileNameWithoutExtension(path) ?? string.Empty;
        var serials = SerialRx.Matches(fileName)
            .Select(match => match.Groups["v"].Value.ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        string? title = fileName;
        if (serials.Length > 0)
        {
            var match = SerialRx.Match(fileName);
            title = fileName[..match.Index].Trim();
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            title = null;
        }

        var bindings = ParseIdentityBindings(fileName);
        var versions = bindings
            .Where(binding => binding.EffectiveVersion is not null)
            .Select(binding => binding.EffectiveVersion!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new NclSourceIdentity(
            Path.GetFileName(path), title, serials, versions)
        {
            TargetBindings = bindings,
            UnresolvedReason = serials.Length == 0
                ? "UNRESOLVED_NO_SERIAL"
                : bindings.All(binding => binding.EffectiveVersion is null)
                    ? "UNRESOLVED_NO_PROVEN_VERSION"
                    : null
        };
    }

    private static NclTargetBinding[] ParseIdentityBindings(string fileName)
    {
        var bindings = new List<NclTargetBinding>();
        var serialTokens = new List<Match>();
        var versionTokens = new List<Match>();
        var evidence = new List<NclIdentityEvidence>();
        var ambiguous = false;
        // Numbers in a game title are not version evidence for its serials.
        var start = SerialRx.Match(fileName);
        if (!start.Success)
        {
            return [];
        }

        foreach (Match token in IdentityTokenRx.Matches(fileName, start.Index))
        {
            if (token.Groups["serial"].Success)
            {
                if (versionTokens.Count > 0 || ambiguous)
                {
                    Flush();
                }
                serialTokens.Add(token);
            }
            else if (token.Groups["unknown"].Success)
            {
                // A suffix after complete identity does not rewrite that
                // identity. Unknown text within a serial group is ambiguous.
                if (versionTokens.Count > 0)
                {
                    Flush();
                }
                else
                {
                    ambiguous = true;
                }
            }
            else
            {
                versionTokens.Add(token);
            }

            evidence.Add(new NclIdentityEvidence(token.Value, token.Index,
                token.Length, token.Groups["serial"].Success ? "Serial"
                    : token.Groups["app"].Success ? "AppVersion"
                    : token.Groups["disc"].Success ? "SourceVersion"
                    : token.Groups["bare"].Success ? "BareVersion" : "Unknown"));
        }
        Flush();
        return bindings.ToArray();

        void Flush()
        {
            // Normalized through PatVer so "1.00" and "01.00" are one version
            // rather than two competing ones. The raw token stays in Evidence.
            var sourceVersions = versionTokens
                .Where(token => !token.Groups["app"].Success)
                .Select(token => PatVer.Norm(token.Groups["disc"].Success
                    ? token.Groups["discv"].Value : token.Groups["barev"].Value))
                .Where(value => value is not null)
                .Select(value => value!)
                .Distinct(StringComparer.Ordinal).ToArray();
            var appVersions = versionTokens
                .Where(token => token.Groups["app"].Success)
                .Select(token => PatVer.Norm(token.Groups["appv"].Value))
                .Where(value => value is not null)
                .Select(value => value!)
                .Distinct(StringComparer.Ordinal).ToArray();
            // A cross product of source and app versions is not mapping evidence.
            var reason = ambiguous || sourceVersions.Length > 1 || appVersions.Length > 1
                ? "UNRESOLVED_AMBIGUOUS_VERSION_MAPPING"
                : versionTokens.Count == 0 ? "UNRESOLVED_NO_VERSION" : null;
            foreach (var serial in serialTokens.Select(token => token.Value.ToUpperInvariant())
                         .Distinct(StringComparer.Ordinal))
            {
                bindings.Add(new NclTargetBinding(serial,
                    sourceVersions.Length == 1 ? sourceVersions[0] : null,
                    appVersions.Length == 1 ? appVersions[0] : null,
                    evidence.ToArray(), reason));
            }
            serialTokens.Clear();
            versionTokens.Clear();
            evidence.Clear();
            ambiguous = false;
        }
    }

    private static NclDoc ParseModernNetCheat(
        string text,
        string? sourcePath)
    {
        var docDiags = new List<NclDiag>();
        var cheats = new List<NclCheat>();
        var lineBase = 1;

        foreach (var block in text.Split('#'))
        {
            var blockLines = block.Split('\n');
            var indexed = blockLines
                .Select((value, index) => (Value: value.Trim(), Line: lineBase + index))
                .Where(item => item.Value.Length > 0)
                .ToArray();
            lineBase += blockLines.Length;

            if (indexed.Length == 0)
            {
                continue;
            }

            if (indexed.Length < 3)
            {
                docDiags.Add(new NclDiag(
                    NclDiagSev.Error,
                    "NCL001",
                    "Modern NetCheat NCL block requires name, state, and author lines.",
                    indexed[0].Line));
                continue;
            }

            var name = indexed[0].Value;
            var stateText = indexed[1].Value;
            var author = indexed[2].Value;
            var cheatDiags = new List<NclDiag>();
            var constant = false;

            if (stateText is "0" or "1")
            {
                constant = stateText == "1";
            }
            else
            {
                cheatDiags.Add(new NclDiag(
                    NclDiagSev.Error,
                    "NCL002",
                    $"Invalid NetCheat constant-write state '{stateText}'.",
                    indexed[1].Line));
            }

            cheats.Add(ParseCheat(
                name,
                constant,
                author,
                indexed.Skip(3).ToArray(),
                cheatDiags,
                NclDialect.NetCheat453,
                []));
        }

        return new NclDoc(
            NclDocFormat.ModernHash,
            cheats,
            docDiags,
            sourcePath,
            NclDialect.NetCheat453);
    }

    private static NclDoc ParseModernArtemis(
        string text,
        string? sourcePath,
        NclOptionSelection? optionSelection)
    {
        var docDiags = new List<NclDiag>();
        var cheats = new List<NclCheat>();

        foreach (var block in SplitArtemisBlocks(text))
        {
            var physicalLines = block.Text.Split('\n');
            var indexed = physicalLines
                .Select((value, index) =>
                    (Value: value.Trim(), Line: block.StartLine + index))
                .Where(item => item.Value.Length > 0)
                .ToArray();

            if (indexed.Length < 2)
            {
                var metadataName = indexed.Length == 1
                    ? indexed[0].Value
                    : "<non-executable>";
                var metadataOnlyNotes = indexed.Select(item => item.Value).ToArray();
                cheats.Add(new NclCheat(
                    metadataName,
                    false,
                    null,
                    [],
                    [],
                    metadataOnlyNotes,
                    [new NclDiag(
                        NclDiagSev.Info,
                        "NCLA001",
                        "Artemis source segment has no executable name/state/code structure and is retained as non-executable metadata.",
                        indexed.Length == 0 ? block.StartLine : indexed[0].Line)],
                    NclDialect.ArtemisPs3));
                continue;
            }

            var name = indexed[0].Value;
            var stateText = indexed[1].Value;
            var constant = IsArtemisConstantWrite(stateText);
            var body = indexed.Skip(2).ToArray();
            var bodyText = string.Join('\n', body.Select(item => item.Value));
            var options = ParseArtemisOptions(bodyText);

            (string Value, int Line)[] shrunkBody;
            IReadOnlyList<string> metadataNotes;

            // Capital Z is not an Artemis hex character, so an unresolved
            // placeholder line is dropped by ArtemisCodeLineValidity. Option
            // substitution therefore has to happen on the raw body, before
            // ShrinkArtemisBody sees it. A failed selection has to reach this
            // cheat's own diagnostics so the cheat itself fails closed.
            var cheatDiags = new List<NclDiag>();
            var resolvedBody = ResolveArtemisOptionBody(
                bodyText,
                body,
                options,
                optionSelection,
                cheats.Count,
                cheatDiags,
                out var requiredOptionIds);

            if (resolvedBody is null)
            {
                shrunkBody = [];
                metadataNotes = body
                    .Select(item => item.Value)
                    .ToArray();
            }
            else
            {
                shrunkBody = ShrinkArtemisBody(
                    resolvedBody,
                    out metadataNotes);

                // The tags are metadata once resolved: they must never become
                // patch instructions, and the resolved instance is classified
                // by the normal verifier rules.
                options = [];
            }

            var notes = metadataNotes;

            cheats.Add(ParseCheat(
                name,
                constant,
                null,
                shrunkBody,
                cheatDiags,
                NclDialect.ArtemisPs3,
                options,
                notes) with
            {
                RequiredOptionPlaceholderIds = requiredOptionIds
            });
        }

        return new NclDoc(
            NclDocFormat.ModernHash,
            cheats,
            docDiags,
            sourcePath,
            NclDialect.ArtemisPs3);
    }

    private static NclDoc ParseLegacy(
        string text,
        string? sourcePath,
        NclDialect dialect,
        NclOptionSelection? optionSelection)
    {
        var docDiags = new List<NclDiag>();
        var cheats = new List<NclCheat>();
        var lines = text.Split('\n');
        var index = 0;

        while (index < lines.Length)
        {
            if (lines[index].Trim() != "{")
            {
                index++;
                continue;
            }

            var start = index + 1;
            index++;
            var body = new List<(string Value, int Line)>();
            while (index < lines.Length && lines[index].Trim() != "}")
            {
                if (!string.IsNullOrWhiteSpace(lines[index]))
                {
                    body.Add((lines[index].Trim(), index + 1));
                }
                index++;
            }

            if (index >= lines.Length)
            {
                docDiags.Add(new NclDiag(
                    NclDiagSev.Error,
                    "NCL003",
                    "Legacy NCL block is missing closing brace.",
                    start));
                break;
            }

            index++;
            if (body.Count < 2)
            {
                docDiags.Add(new NclDiag(
                    NclDiagSev.Error,
                    "NCL004",
                    "Legacy NCL block requires state and name.",
                    start));
                continue;
            }

            bool constant;
            if (dialect == NclDialect.ArtemisPs3)
            {
                constant = IsArtemisConstantWrite(body[0].Value);
            }
            else if (!bool.TryParse(body[0].Value, out constant))
            {
                docDiags.Add(new NclDiag(
                    NclDiagSev.Error,
                    "NCL005",
                    $"Invalid legacy constant-write state '{body[0].Value}'.",
                    body[0].Line));
                continue;
            }

            var codeBody = body.Skip(2).ToArray();
            List<NclOptionSet> options = [];
            IReadOnlyList<string>? artemisNotes = null;
            var cheatDiags = new List<NclDiag>();
            IReadOnlyList<int> requiredOptionIds = [];
            if (dialect == NclDialect.ArtemisPs3)
            {
                var codeBodyText = string.Join(
                    '\n',
                    codeBody.Select(item => item.Value));
                options = ParseArtemisOptions(codeBodyText);

                var resolvedCodeBody = ResolveArtemisOptionBody(
                    codeBodyText,
                    codeBody,
                    options,
                    optionSelection,
                    cheats.Count,
                    cheatDiags,
                    out requiredOptionIds);

                if (resolvedCodeBody is null)
                {
                    artemisNotes = codeBody.Select(item => item.Value).ToArray();
                    codeBody = [];
                }
                else
                {
                    codeBody = ShrinkArtemisBody(
                        resolvedCodeBody,
                        out artemisNotes);
                    options = [];
                }
            }

            cheats.Add(ParseCheat(
                body[1].Value,
                constant,
                null,
                codeBody,
                cheatDiags,
                dialect,
                options,
                artemisNotes) with
            {
                RequiredOptionPlaceholderIds = requiredOptionIds
            });
        }

        if (cheats.Count == 0 && docDiags.Count == 0 && !string.IsNullOrWhiteSpace(text))
        {
            docDiags.Add(new NclDiag(
                NclDiagSev.Error,
                "NCL006",
                "NCL text is neither modern hash-delimited nor legacy brace format."));
        }

        return new NclDoc(
            NclDocFormat.LegacyBrace,
            cheats,
            docDiags,
            sourcePath,
            dialect);
    }

    private static NclCheat ParseCheat(
        string name,
        bool constant,
        string? author,
        (string Value, int Line)[] body,
        List<NclDiag> diags,
        NclDialect dialect,
        IReadOnlyList<NclOptionSet> initialOptions,
        IReadOnlyList<string>? initialNotes = null)
    {
        var ops = new List<NclOp>();
        var options = new List<NclOptionSet>(initialOptions);
        var notes = initialNotes is null
            ? new List<string>()
            : new List<string>(initialNotes);

        for (var index = 0; index < body.Length; index++)
        {
            var (raw, line) = body[index];
            if (dialect == NclDialect.NetCheat453 &&
                TryNetCheatOption(raw, out var option))
            {
                options.Add(option);
                continue;
            }

            if (dialect == NclDialect.NetCheat453 && IsComment(raw))
            {
                notes.Add(raw);
                continue;
            }

            if (!TryTokens(raw, out var command, out var arg1, out var arg2))
            {
                notes.Add(raw);
                continue;
            }

            var kind = char.ToUpperInvariant(command[0]);
            var modifierText = command.Length > 1 ? command[1..] : string.Empty;
            if (!TryHexUInt(modifierText, out var modifier))
            {
                diags.Add(Error("NCL010", "Invalid opcode modifier.", line));
                ops.Add(new NclUnsupportedOp(line, raw, "Invalid opcode modifier."));
                continue;
            }

            switch (kind)
            {
                case '0':
                    if (!TryHexULong(arg1, out var address) ||
                        !TryHexBytes(arg2, out var directValue))
                    {
                        AddInvalid(
                            diags,
                            ops,
                            line,
                            raw,
                            "NCL011",
                            "Invalid direct-write address or byte value.");
                        break;
                    }

                    ops.Add(new NclDirectWrite(
                        line,
                        raw,
                        address,
                        directValue,
                        modifier switch
                        {
                            1 => NclWriteMode.Or,
                            2 => NclWriteMode.And,
                            3 => NclWriteMode.Xor,
                            _ => NclWriteMode.Replace
                        },
                        modifier));
                    break;

                case '1':
                    if (modifier != 0 || !TryHexULong(arg1, out address))
                    {
                        AddInvalid(
                            diags,
                            ops,
                            line,
                            raw,
                            "NCL012",
                            "Text write requires opcode 1 and a hexadecimal address.");
                        break;
                    }

                    var textBytes = Encoding.UTF8.GetBytes(arg2);
                    byte[] encodedText = dialect == NclDialect.NetCheat453
                        ? [.. textBytes, (byte)0]
                        : textBytes;
                    ops.Add(new NclTextWrite(
                        line,
                        raw,
                        address,
                        arg2,
                        encodedText,
                        dialect == NclDialect.NetCheat453));
                    break;

                case '2':
                    if (modifier != 0 ||
                        !TryHexULong(arg1, out address) ||
                        !float.TryParse(
                            arg2,
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out var floatValue) ||
                        // .NET Core 3.0+ parses an out-of-range literal as
                        // +/-Infinity instead of failing. NaN/Infinity are
                        // never a proven Float32 cheat value, so a non-finite
                        // result is rejected rather than silently written.
                        !float.IsFinite(floatValue))
                    {
                        AddInvalid(
                            diags,
                            ops,
                            line,
                            raw,
                            "NCL013",
                            "Float write requires opcode 2, hexadecimal address, and invariant float value.");
                        break;
                    }

                    var floatBytes = BitConverter.GetBytes(floatValue);
                    if (BitConverter.IsLittleEndian)
                    {
                        Array.Reverse(floatBytes);
                    }

                    ops.Add(new NclFloatWrite(
                        line,
                        raw,
                        address,
                        floatValue,
                        floatBytes));
                    break;

                case '4':
                    if (modifier != 0 ||
                        !TryHexULong(arg1, out address) ||
                        !TryHexBytes(arg2, out var initialValue))
                    {
                        AddInvalid(
                            diags,
                            ops,
                            line,
                            raw,
                            "NCL014",
                            "Invalid first line of condensed write.");
                        break;
                    }

                    if (index + 1 >= body.Length ||
                        !TryTokens(
                            body[index + 1].Value,
                            out var secondCommand,
                            out var secondArg1,
                            out var secondArg2) ||
                        char.ToUpperInvariant(secondCommand[0]) != '4' ||
                        !TryHexUInt(
                            secondCommand.Length > 1
                                ? secondCommand[1..]
                                : string.Empty,
                            out var valueIncrement) ||
                        !TryHexULong(secondArg1, out var addressIncrement) ||
                        !TryHexULong(secondArg2, out var repeatCount))
                    {
                        AddInvalid(
                            diags,
                            ops,
                            line,
                            raw,
                            "NCL015",
                            "Condensed write requires a valid second 4[VINC] line.");
                        break;
                    }

                    ops.Add(new NclCondensedWrite(
                        line,
                        raw,
                        address,
                        initialValue,
                        addressIncrement,
                        repeatCount,
                        valueIncrement,
                        body[index + 1].Value));
                    index++;
                    break;

                case '6':
                    if (!TryHexULong(arg1, out var pointerAddress) ||
                        !TryHexULong(arg2, out var pointerOffset))
                    {
                        AddInvalid(
                            diags,
                            ops,
                            line,
                            raw,
                            "NCL016",
                            "Invalid pointer address or offset.");
                        break;
                    }

                    ops.Add(new NclPointerResolve(
                        line,
                        raw,
                        pointerAddress,
                        pointerOffset,
                        dialect == NclDialect.NetCheat453 && modifier != 0,
                        modifier));
                    break;

                case 'A':
                    if (modifier is not (1 or 2) ||
                        !TryHexULong(arg1, out address) ||
                        !TryHexULong(arg2, out var count))
                    {
                        AddInvalid(
                            diags,
                            ops,
                            line,
                            raw,
                            "NCL017",
                            "A opcode requires A1/A2, address, and byte count.");
                        break;
                    }

                    ops.Add(new NclCopyBuffer(
                        line,
                        raw,
                        address,
                        count,
                        modifier == 2,
                        modifier));
                    break;

                case 'B':
                    if (!TryHexULong(arg1, out var start) ||
                        !TryHexULong(arg2, out var stop))
                    {
                        AddInvalid(
                            diags,
                            ops,
                            line,
                            raw,
                            "NCL018",
                            "Invalid find/replace range.");
                        break;
                    }

                    if (index + 1 >= body.Length ||
                        !TryTokens(
                            body[index + 1].Value,
                            out var findCommand,
                            out var originalText,
                            out var replacementText) ||
                        !findCommand.Equals("B", StringComparison.OrdinalIgnoreCase) ||
                        !TryHexBytes(originalText, out var original) ||
                        !TryHexBytes(replacementText, out var replacement) ||
                        original.Length == 0 ||
                        replacement.Length == 0)
                    {
                        AddInvalid(
                            diags,
                            ops,
                            line,
                            raw,
                            "NCL019",
                            "Find/replace requires a second B line with non-empty hexadecimal original and replacement byte patterns.");
                        break;
                    }

                    ops.Add(new NclFindReplace(
                        line,
                        raw,
                        start,
                        stop,
                        modifier,
                        original,
                        replacement,
                        body[index + 1].Value));
                    index++;
                    break;

                case 'D':
                    if (!TryHexULong(arg1, out address) ||
                        !TryHexBytes(arg2, out var expected))
                    {
                        AddInvalid(
                            diags,
                            ops,
                            line,
                            raw,
                            "NCL020",
                            "Invalid equal conditional.");
                        break;
                    }

                    ops.Add(new NclConditionalEqual(
                        line,
                        raw,
                        address,
                        modifier,
                        expected));
                    break;

                case 'E':
                    if (!TryHexULong(arg1, out address) ||
                        !TryHexBytes(arg2, out var mask))
                    {
                        AddInvalid(
                            diags,
                            ops,
                            line,
                            raw,
                            "NCL021",
                            "Invalid mask conditional.");
                        break;
                    }

                    ops.Add(new NclConditionalMaskUnset(
                        line,
                        raw,
                        address,
                        modifier,
                        mask));
                    break;

                case 'F':
                    if (modifier == 0 ||
                        !TryHexULong(arg1, out var source) ||
                        !TryHexULong(arg2, out var destination))
                    {
                        AddInvalid(
                            diags,
                            ops,
                            line,
                            raw,
                            "NCL022",
                            "F opcode requires non-zero hex byte count, source, and destination.");
                        break;
                    }

                    ops.Add(new NclCopyBytes(
                        line,
                        raw,
                        source,
                        destination,
                        modifier));
                    break;

                default:
                    ops.Add(new NclUnsupportedOp(
                        line,
                        raw,
                        $"Unsupported NCL opcode '{command}'."));
                    diags.Add(new NclDiag(
                        NclDiagSev.Warning,
                        "NCL023",
                        $"Unsupported NCL opcode '{command}' preserved for rejection.",
                        line));
                    break;
            }
        }

        ValidateCompoundCounts(ops, diags);

        return new NclCheat(
            name,
            constant,
            string.IsNullOrWhiteSpace(author) ? null : author,
            ops,
            options,
            notes,
            diags,
            dialect);
    }

    private static void ValidateCompoundCounts(
        List<NclOp> ops,
        List<NclDiag> diags)
    {
        for (var index = 0; index < ops.Count; index++)
        {
            ulong controlled = ops[index] switch
            {
                NclConditionalEqual equal => equal.ControlledLineCount,
                NclConditionalMaskUnset mask => mask.ControlledLineCount,
                _ => 0UL
            };

            if (controlled > 0 && controlled > (ulong)(ops.Count - index - 1))
            {
                diags.Add(new NclDiag(
                    NclDiagSev.Error,
                    "NCL024",
                    $"Conditional controls {controlled} operation(s), but fewer remain.",
                    ops[index].Line));
            }
        }
    }

    // Returns the body to parse, or null when the cheat keeps its unresolved
    // option state. A cheat without option tags is returned unchanged so the
    // existing non-option path is byte-for-byte identical.
    private static (string Value, int Line)[]? ResolveArtemisOptionBody(
        string bodyText,
        (string Value, int Line)[] body,
        IReadOnlyList<NclOptionSet> options,
        NclOptionSelection? optionSelection,
        int cheatIndex,
        List<NclDiag> diags,
        out IReadOnlyList<int> requiredPlaceholderIds)
    {
        requiredPlaceholderIds = [];

        if (options.Count == 0)
        {
            return body;
        }

        // Record what the raw body references while the raw body is in hand.
        // Only a cheat that merely awaits a selection reports ids, so ambiguous,
        // undeclared and unreferenced option sets stay closed.
        var probe = NclOptionSubstitution.Resolve(bodyText, options, null);
        if (probe.Code == NclOptionResolveCode.SelectionRequired)
        {
            requiredPlaceholderIds = probe.RequiredPlaceholderIds;
        }

        if (optionSelection is null ||
            optionSelection.CheatIndex != cheatIndex)
        {
            return null;
        }

        var resolved = NclOptionSubstitution.Resolve(
            bodyText,
            options,
            optionSelection.ValuesByPlaceholderId);

        if (!resolved.IsOk)
        {
            // A selection was supplied for this cheat and could not be honoured.
            // Ambiguous, undeclared and invalid selections fail closed instead of
            // silently falling back to the unresolved state.
            if (resolved.Code != NclOptionResolveCode.SelectionRequired)
            {
                diags.Add(new NclDiag(
                    NclDiagSev.Error,
                    "NCL020",
                    resolved.Error ??
                    "The supplied NCL option selection could not be resolved.",
                    body.Length > 0 ? body[0].Line : (int?)null));
            }

            return null;
        }

        var resolvedLines = resolved.ResolvedText.Split('\n');
        if (resolvedLines.Length != body.Length)
        {
            diags.Add(new NclDiag(
                NclDiagSev.Error,
                "NCL021",
                "Option substitution changed the NCL source line structure.",
                body.Length > 0 ? body[0].Line : (int?)null));
            return null;
        }

        var output = new (string Value, int Line)[body.Length];
        for (var index = 0; index < body.Length; index++)
        {
            output[index] = (resolvedLines[index], body[index].Line);
        }

        return output;
    }

    private static List<NclOptionSet> ParseArtemisOptions(string rawBody)
    {
        if (string.IsNullOrWhiteSpace(rawBody))
        {
            return [];
        }

        var result = new List<NclOptionSet>();
        foreach (Match match in ArtemisOptionRx.Matches(rawBody))
        {
            var id = match.Groups["id"].Value.Length;
            var values = match.Groups["body"].Value
                .TrimStart(']')
                .Split(
                    ';',
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries)
                .Select(item => item.Split(
                    '=',
                    2,
                    StringSplitOptions.TrimEntries))
                .Where(parts =>
                    parts.Length == 2 &&
                    parts[0].Length > 0 &&
                    parts[1].Length > 0)
                .Select(parts => new NclOption(parts[0], parts[1]))
                .ToArray();

            result.Add(new NclOptionSet(
                match.Value,
                values,
                id));
        }

        return result;
    }

    private static bool TryNetCheatOption(
        string raw,
        out NclOptionSet option)
    {
        option = new NclOptionSet(raw, []);
        if (!raw.StartsWith("[Z]", StringComparison.OrdinalIgnoreCase) ||
            !raw.EndsWith("[/Z]", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var inner = raw[3..^4];
        var values = inner
            .Split(
                ';',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries)
            .Select(item => item.Split(
                '=',
                2,
                StringSplitOptions.TrimEntries))
            .Where(parts =>
                parts.Length == 2 &&
                parts[0].Length > 0 &&
                parts[1].Length > 0)
            .Select(parts => new NclOption(parts[0], parts[1]))
            .ToArray();
        option = new NclOptionSet(raw, values);
        return true;
    }

    private static (string Value, int Line)[] ShrinkArtemisBody(
        (string Value, int Line)[] body,
        out IReadOnlyList<string> metadataNotes)
    {
        if (body.Length == 0)
        {
            metadataNotes = [];
            return [];
        }

        var startLine = body[0].Line;
        var joined = string.Join('\n', body.Select(item => item.Value));
        var withoutMultilineComments = StripMultilineCommentsPreserveLines(joined);
        var output = new List<(string Value, int Line)>();
        var notes = new List<string>();
        var lines = withoutMultilineComments.Split('\n');

        for (var index = 0; index < lines.Length; index++)
        {
            var raw = lines[index].Trim();
            if (raw.Length == 0)
            {
                continue;
            }

            var validity = ArtemisCodeLineValidity(raw);
            if (validity == 0)
            {
                notes.Add(raw);
                continue;
            }

            var executable = validity == 2
                ? TrimArtemisTrailingText(raw)
                : raw;
            executable = executable.Trim();
            if (executable.Length > 0)
            {
                output.Add((executable, startLine + index));
            }
        }

        metadataNotes = notes;
        return output.ToArray();
    }

    private static string StripMultilineCommentsPreserveLines(string text)
    {
        var output = new StringBuilder(text.Length);
        var index = 0;
        var inComment = false;

        while (index < text.Length)
        {
            if (!inComment &&
                index + 1 < text.Length &&
                text[index] == '/' &&
                text[index + 1] == '*')
            {
                inComment = true;
                index += 2;
                continue;
            }

            if (inComment &&
                index + 1 < text.Length &&
                text[index] == '*' &&
                text[index + 1] == '/')
            {
                inComment = false;
                index += 2;
                continue;
            }

            if (!inComment)
            {
                output.Append(text[index]);
            }
            else if (text[index] == '\n')
            {
                output.Append('\n');
            }

            index++;
        }

        return output.ToString();
    }

    private static int ArtemisCodeLineValidity(string line)
    {
        if (line.Length < 2)
        {
            return 0;
        }

        var spaceCount = line.Count(character => character == ' ');
        var textSpaceCount = 0;
        var isText = line[0] == '1' && line[1] == ' ';
        var isFloat = line[0] == '2' && line[1] == ' ';

        if (!isText && !isFloat)
        {
            if (spaceCount != 2)
            {
                return 0;
            }

            foreach (var character in line)
            {
                if (character == ' ')
                {
                    textSpaceCount++;
                }

                if (!IsArtemisHexCharacter(character))
                {
                    return textSpaceCount > 2 ? 2 : 0;
                }
            }

            return 1;
        }

        if (isText)
        {
            if (spaceCount < 2)
            {
                return 0;
            }

            foreach (var character in line)
            {
                if (character == ' ')
                {
                    textSpaceCount++;
                }

                if (textSpaceCount >= 2)
                {
                    break;
                }

                if (!IsArtemisHexCharacter(character))
                {
                    return 0;
                }
            }

            return 1;
        }

        if (spaceCount != 2)
        {
            return 0;
        }

        foreach (var character in line)
        {
            if (character == ' ')
            {
                textSpaceCount++;
            }

            if (!IsArtemisHexCharacter(character) &&
                character is not ('.' or '-'))
            {
                return textSpaceCount > 2 ? 2 : 0;
            }
        }

        return 1;
    }

    private static bool IsArtemisHexCharacter(char character)
    {
        var normalized = character >= 0x61
            ? (char)(character - 0x20)
            : character;

        return normalized is
            'A' or 'B' or 'C' or 'D' or 'E' or 'F' or
            ' ' or '\r' or '\n' or
            '0' or '1' or '2' or '3' or '4' or
            '5' or '6' or '7' or '8' or '9';
    }

    private static string TrimArtemisTrailingText(string line)
    {
        var spaces = 0;
        for (var index = 0; index < line.Length; index++)
        {
            if (line[index] != ' ')
            {
                continue;
            }

            spaces++;
            if (spaces == 3)
            {
                return line[..index];
            }
        }

        return line;
    }

    private static bool IsArtemisConstantWrite(string stateText)
    {
        var trimmed = stateText.TrimStart();
        return trimmed.Length > 0 && trimmed[0] is '1' or 'T' or 't';
    }

    private static List<ArtemisBlock> SplitArtemisBlocks(string text)
    {
        var blocks = new List<ArtemisBlock>();
        var buffer = new StringBuilder();
        var index = 0;
        var line = 1;
        var startLine = 1;

        while (index < text.Length)
        {
            var character = text[index];
            if (character == '#')
            {
                blocks.Add(new ArtemisBlock(
                    buffer.ToString(),
                    startLine));
                buffer.Clear();

                while (index < text.Length && text[index] != '\n')
                {
                    index++;
                }

                if (index < text.Length && text[index] == '\n')
                {
                    line++;
                }

                startLine = line;
                index++;
                continue;
            }

            buffer.Append(character);
            if (character == '\n')
            {
                line++;
            }

            index++;
        }

        if (!IsWhiteSpaceOnly(buffer))
        {
            blocks.Add(new ArtemisBlock(
                buffer.ToString(),
                startLine));
        }

        return blocks;
    }

    private static bool IsWhiteSpaceOnly(StringBuilder buffer)
    {
        for (var index = 0; index < buffer.Length; index++)
        {
            if (!char.IsWhiteSpace(buffer[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsComment(string raw) =>
        raw.StartsWith("//", StringComparison.Ordinal) ||
        raw.StartsWith("/*", StringComparison.Ordinal) ||
        raw.StartsWith('*');

    private static bool TryTokens(
        string raw,
        out string command,
        out string arg1,
        out string arg2)
    {
        command = string.Empty;
        arg1 = string.Empty;
        arg2 = string.Empty;
        var parts = raw.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
        {
            return false;
        }

        command = parts[0];
        arg1 = parts[1];
        arg2 = parts[2];
        return command.Length > 0;
    }

    private static bool TryHexUInt(string text, out uint value)
    {
        if (string.IsNullOrEmpty(text))
        {
            value = 0;
            return true;
        }

        return uint.TryParse(
            text,
            NumberStyles.HexNumber,
            CultureInfo.InvariantCulture,
            out value);
    }

    private static bool TryHexULong(string text, out ulong value) =>
        ulong.TryParse(
            text,
            NumberStyles.HexNumber,
            CultureInfo.InvariantCulture,
            out value);

    private static bool TryHexBytes(string text, out byte[] bytes)
    {
        bytes = [];
        if (string.IsNullOrWhiteSpace(text) || (text.Length & 1) != 0)
        {
            return false;
        }

        var result = new byte[text.Length / 2];
        for (var index = 0; index < result.Length; index++)
        {
            if (!byte.TryParse(
                    text.AsSpan(index * 2, 2),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture,
                    out result[index]))
            {
                return false;
            }
        }

        bytes = result;
        return true;
    }

    private static void AddInvalid(
        List<NclDiag> diags,
        List<NclOp> ops,
        int line,
        string raw,
        string code,
        string message)
    {
        diags.Add(Error(code, message, line));
        ops.Add(new NclUnsupportedOp(line, raw, message));
    }

    private static NclDiag Error(
        string code,
        string message,
        int line) =>
        new(NclDiagSev.Error, code, message, line);

    private sealed record ArtemisBlock(
        string Text,
        int StartLine);
}
