using System.Text;
using System.Text.RegularExpressions;
using R3Config.Mdl;

namespace R3Config.Svc;

// Artemis/NetCheat option placeholders are a source-text substitution performed
// before code parsing, not a runtime opcode.
//
// ArtemisPS3-GUI source/codes.c (bucanero/ArtemisPS3):
//   ReadOptions()        takes the tag id from the run of 'Z' after '[',
//   ReadOptionFromLine() splits the tag body into value=name pairs on ';'/'=',
//   ParseOptionsCode()   walks every option set of a code and calls
//   ParseOptionsID()     which builds a string of exactly <id> 'Z' characters
//                        and replaces every instance of it in the code buffer
//                        with the selected value.
//   replace_str_count()  skips a match whose following character is also 'Z',
//                        so a shorter run never matches inside a longer one.
//
// This service reproduces that substitution and nothing else. It never chooses
// a value, never infers width or endianness from the tag, and never classifies
// the result: the substituted text goes back through the existing parser,
// semantic verifier, static lowerer, address binder and emitter unchanged.
public static partial class NclOptionSubstitution
{
    private const int MaxBodyChars = 2 * 1024 * 1024;
    private const int MaxSubstitutions = 4096;

    public static NclOptionResolveResult Resolve(
        string cheatBodyText,
        IReadOnlyList<NclOptionSet> optionSets,
        IReadOnlyDictionary<int, string>? selections)
    {
        ArgumentNullException.ThrowIfNull(cheatBodyText);
        ArgumentNullException.ThrowIfNull(optionSets);

        if (cheatBodyText.Length > MaxBodyChars)
        {
            return Failure(
                NclOptionResolveCode.InvalidInput,
                $"Option substitution requires a cheat body of at most {MaxBodyChars} characters.");
        }

        // The parser and this service must agree on the option tags they see. A
        // disagreement means the caller passed a body the option sets were not
        // parsed from, which is never safe to substitute into.
        if (OptionTagRegex().Matches(cheatBodyText).Count != optionSets.Count)
        {
            return Failure(
                NclOptionResolveCode.InvalidInput,
                "The supplied option sets do not correspond to the option tags in this cheat body.");
        }

        // Exactly one declared set per placeholder id. Upstream would apply both
        // sets in declaration order; R3Config emits a persistent patch instead of
        // driving a live GUI, so ambiguous ownership fails closed.
        var declared = new Dictionary<int, NclOptionSet>();
        foreach (var set in optionSets)
        {
            if (set is null ||
                set.PlaceholderId <= 0)
            {
                return Failure(
                    NclOptionResolveCode.InvalidInput,
                    "An option set has no valid placeholder id.");
            }

            if (!declared.TryAdd(set.PlaceholderId, set))
            {
                return Failure(
                    NclOptionResolveCode.AmbiguousOptionSet,
                    $"Placeholder id {set.PlaceholderId} is declared by more than one option set.");
            }
        }

        // Option tags and block comments are metadata: a 'Z' inside either is
        // never a code placeholder, and the tags must not reach the emitter.
        var substitutable = BuildSubstitutableMask(cheatBodyText);

        var runs = new List<(int Start, int Length)>();
        var required = new SortedSet<int>();
        var index = 0;

        while (index < cheatBodyText.Length)
        {
            if (cheatBodyText[index] != 'Z' ||
                !substitutable[index])
            {
                index++;
                continue;
            }

            // A maximal contiguous run of capital 'Z' is one placeholder, so a
            // 'Z' run is never matched inside a 'ZZ' run.
            var start = index;
            while (index < cheatBodyText.Length &&
                   cheatBodyText[index] == 'Z' &&
                   substitutable[index])
            {
                index++;
            }

            runs.Add((start, index - start));
            required.Add(index - start);

            if (runs.Count > MaxSubstitutions)
            {
                return Failure(
                    NclOptionResolveCode.SubstitutionLimit,
                    $"The cheat body exceeds the {MaxSubstitutions} placeholder substitution limit.");
            }
        }

        var requiredIds = required.ToArray();

        if (runs.Count == 0)
        {
            // A declared but unreferenced option set leaves the code complete and
            // deterministic: there is nothing to select and nothing to replace.
            return new NclOptionResolveResult(
                true,
                NclOptionResolveCode.Success,
                cheatBodyText,
                [],
                [],
                0,
                null);
        }

        foreach (var id in requiredIds)
        {
            if (!declared.ContainsKey(id))
            {
                return Failure(
                    NclOptionResolveCode.UnknownPlaceholder,
                    $"The code references placeholder id {id}, which no option set declares.",
                    requiredIds);
            }
        }

        var missing = requiredIds
            .Where(id =>
                selections is null ||
                !selections.ContainsKey(id))
            .ToArray();

        if (missing.Length > 0)
        {
            return Failure(
                NclOptionResolveCode.SelectionRequired,
                "Every referenced option placeholder requires an explicit selection.",
                requiredIds,
                missing);
        }

        foreach (var id in requiredIds)
        {
            var value = selections![id];

            // A value is substituted verbatim, so it may be a spaced value or a
            // complete code line, but it may never change the line structure the
            // parser reports diagnostics against.
            if (string.IsNullOrEmpty(value) ||
                value.Contains('\n', StringComparison.Ordinal) ||
                value.Contains('\r', StringComparison.Ordinal) ||
                !declared[id].Values.Any(item =>
                    string.Equals(
                        item.Value,
                        value,
                        StringComparison.Ordinal)))
            {
                return Failure(
                    NclOptionResolveCode.InvalidSelection,
                    $"The selected value for placeholder id {id} is not a single-line value declared by its option set.",
                    requiredIds);
            }
        }

        var builder = new StringBuilder(cheatBodyText.Length);
        var previous = 0;

        foreach (var (start, length) in runs)
        {
            builder.Append(cheatBodyText, previous, start - previous);
            builder.Append(selections![length]);
            previous = start + length;
        }

        builder.Append(
            cheatBodyText,
            previous,
            cheatBodyText.Length - previous);

        return new NclOptionResolveResult(
            true,
            NclOptionResolveCode.Success,
            builder.ToString(),
            requiredIds,
            [],
            runs.Count,
            null);
    }

    private static bool[] BuildSubstitutableMask(string text)
    {
        var mask = new bool[text.Length];
        Array.Fill(mask, true);

        foreach (Match match in OptionTagRegex().Matches(text))
        {
            Clear(mask, match.Index, match.Length);
        }

        foreach (Match match in BlockCommentRegex().Matches(text))
        {
            Clear(mask, match.Index, match.Length);
        }

        return mask;
    }

    private static void Clear(
        bool[] mask,
        int start,
        int length)
    {
        for (var index = start; index < start + length; index++)
        {
            mask[index] = false;
        }
    }

    private static NclOptionResolveResult Failure(
        NclOptionResolveCode code,
        string error,
        IReadOnlyList<int>? required = null,
        IReadOnlyList<int>? missing = null)
    {
        return new NclOptionResolveResult(
            false,
            code,
            string.Empty,
            required ?? [],
            missing ?? [],
            0,
            error);
    }

    [GeneratedRegex(
        @"\[(?<id>Z+)\](?<body>.*?)\[/Z+\]",
        RegexOptions.CultureInvariant |
        RegexOptions.IgnoreCase |
        RegexOptions.Singleline)]
    private static partial Regex OptionTagRegex();

    [GeneratedRegex(
        @"/\*.*?\*/",
        RegexOptions.CultureInvariant |
        RegexOptions.Singleline)]
    private static partial Regex BlockCommentRegex();
}
