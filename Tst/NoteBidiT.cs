using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using R3Config.Ctrl;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;
using static R3Config.Tests.TestAssert;

namespace R3Config.Tests;

// Rounds 18 and 20, display only: inside Arabic text every English segment
// (RPCS3 build, version, path, IP/Host, serial, #issue, percent, and a whole
// English title with its brackets) reads LTR exactly as written. WPF reorders
// it otherwise: with ar-SA the digits resolve as Arabic numbers, so
// "v0.0.21-13399" showed as "13399-v0.0.21" (Madden NFL 10, BLUS30375), and a
// bracket or space between two isolated tokens took the RTL paragraph
// direction, so "[Regression] Prototype [BLUS30145] (#19410)" showed as
// "(#19410) [BLUS30145] Regression] Prototype]". LinkText gives each whole
// English segment one LTR en-US Run; no character is inserted, so the text,
// the automation name and every copy stay the source.
internal static class NoteBidiT
{
    // The Arabic Runtime source links verbatim as LogDiagVm builds them from the
    // shipped diagnostics-kb.json ("Diag.Source.GitHubIssue" #number - title).
    internal static readonly (int Number, string Text, string English)[] SourceLinks =
    [
        (19465, "مشكلة GitHub #19465 - [Regression] Prototype [BLUS30145] (#19410)",
            "GitHub #19465 - [Regression] Prototype [BLUS30145] (#19410)"),
        (19448, "مشكلة GitHub #19448 - [Regression] Rock Band 2 [BLUS30147] freezing when joining a band (solo tour)",
            "GitHub #19448 - [Regression] Rock Band 2 [BLUS30147] freezing when joining a band (solo tour)"),
        (19262, "مشكلة GitHub #19262 - [Regression] Various titles fail to boot when connected",
            "GitHub #19262 - [Regression] Various titles fail to boot when connected")
    ];

    // Arabic sentences around whole English segments: nested brackets, a
    // bracket that opens or closes the segment, Arabic on both sides, and a
    // segment that ends an Arabic sentence with its period.
    private static readonly (string Text, string[] English)[] Sentences =
    [
        ("اضبط [Fix (SPU [LLVM]) v2] ثم شغّل اللعبة.", ["[Fix (SPU [LLVM]) v2]"]),
        ("العنوان [Regression] Prototype 200% هو المرجع", ["[Regression] Prototype 200%"]),
        ("راجع Prototype v0.0.21-13399 (#19410) قبل التحديث", ["Prototype v0.0.21-13399 (#19410)"]),
        ("قال المطور Black lines appear in RPCS3 after v0.0.21-13399 (#12514) ثم أصلحها", ["Black lines appear in RPCS3 after v0.0.21-13399 (#12514)"]),
        ("تظهر المشكلة في Rock Band 2 [BLUS30147] freezing when joining a band (solo tour).", ["Rock Band 2 [BLUS30147] freezing when joining a band (solo tour)"])
    ];

    internal const string MaddenEnglish =
        "RPCS3 issue #12514: Black lines appear across the title screen and game menus in RPCS3 versions after v0.0.21-13399.";

    internal const string MaddenArabic =
        "مشكلة RPCS3 رقم #12514: تظهر خطوط سوداء عبر شاشة العنوان وقوائم اللعبة في إصدارات RPCS3 التي تلي v0.0.21-13399.";

    // Arabic sentences built on the token forms of the shipped Arabic notes.
    private static readonly string[] Patterns =
    [
        "أضيف في RPCS3 0.0.41-19440 وما بعده.",
        "يعمل حتى الإصدار 0.0.17-12652؛ بعده يتعطل.",
        "تحتاج تحديث اللعبة 1.09، ثم أعد التشغيل.",
        "ضع الملف في dev_hdd0/game/BLUS30375/USRDIR ثم شغّل اللعبة.",
        "انسخ BLUS31059\\PS3_GAME\\USRDIR\\autoexec.cfg إلى مجلد اللعبة.",
        "الملفات في .../rpcs3/dev_hdd0/music/ تعمل.",
        "اكتب gosredirector.ea.com==95.217.209.57 في الإعداد.",
        "الخادم 185.194.142.4 متوقف.",
        "النسخة NPEA00374/NPUA80756 فقط.",
        "اضبط Resolution Scale على 200% أو +200% ثم 25-35 إطارًا.",
        "نزّل «mod_v2_by_lipsum.7z» من الرابط.",
        "يحل المشكلة PR #18644 (انظر #12514).",
        "افتح RPCS3، ثم اضبط L2/R2."
    ];

    public static void Run()
    {
        // Every whole-segment case is checked before any assert, so one run
        // reports each one that fails.
        RunSta(AssertSegments);
        AssertTokens();
        RunSta(AssertRendered);
        RunSta(AssertShipped);
    }

    // Round 20: the real Runtime source links and the made-up sentences render
    // each English segment whole, in its own order, as one LTR en-US Run.
    private static void AssertSegments()
    {
        var language = UiText.CurLang;
        var failures = new List<string>();
        try
        {
            UiText.SetLang(UiText.ArabLang);
            var knowledge = DiagnosticKnowledgeBase.Load(Path.Combine(AppContext.BaseDirectory, "Data", DiagnosticKnowledgeBase.FileName));
            foreach (var (number, text, english) in SourceLinks)
            {
                var entry = knowledge.Entries.Single(item => item.Source.Number == number);
                var real = string.Create(CultureInfo.InvariantCulture,
                    $"{UiText.GetText("Diag.Source.GitHubIssue")} #{number} - {entry.Source.Title}");
                if (real != text)
                {
                    failures.Add($"#{number}: the Runtime source text changed: {real}");
                    continue;
                }

                foreach (var interactive in new[] { true, false })
                {
                    CheckSegments(Arabic(text, interactive, entry.Source.Url), text, [english], failures);
                }
            }

            foreach (var (text, english) in Sentences)
            {
                CheckSegments(Arabic(text, true), text, english, failures);
            }
        }
        finally
        {
            UiText.SetLang(language);
        }

        Assert(failures.Count == 0, $"{failures.Count} whole-segment failures: {string.Join(" || ", failures)}");
    }

    private static void CheckSegments(TextBlock block, string text, string[] english, List<string> failures)
    {
        var label = $"'{text}' (interactive={LinkText.GetInteractive(block)})";
        var segments = Segments(block).Select(item => item.Text).ToArray();
        if (!segments.SequenceEqual(english))
        {
            failures.Add($"{label}: English segments [{string.Join(" | ", segments)}], expected [{string.Join(" | ", english)}]");
        }

        var reversed = Reversed(block);
        if (reversed.Count > 0)
        {
            failures.Add($"{label}: reads out of order: {string.Join(" | ", reversed)}");
        }

        var runs = Runs(block.Inlines).ToArray();
        foreach (var segment in english)
        {
            if (!runs.Any(run => run.Text == segment && run.FlowDirection == FlowDirection.LeftToRight &&
                                 run.Language.Equals(XmlLanguage.GetLanguage("en-US"))))
            {
                failures.Add($"{label}: '{segment}' is not one LTR en-US Run; runs [{string.Join(" | ", runs.Select(run => run.Text))}]");
            }
        }

        // WPF names each embedded button (the link's "Show link") with one space
        // after the text; nothing else may differ from the source.
        var shown = Shown(block);
        var name = new TextBlockAutomationPeer(block).GetName();
        var buttons = block.Inlines.OfType<InlineUIContainer>().Count();
        if (shown != text || name != text + new string(' ', buttons) ||
            shown.Any(ch => CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.Format))
        {
            failures.Add($"{label}: rendered text or automation name differs from the source. Shown: {shown} Name: {name}");
        }
    }

    private static IEnumerable<Run> Runs(InlineCollection inlines)
    {
        foreach (var inline in inlines)
        {
            if (inline is Run run)
            {
                yield return run;
            }
            else if (inline is Span span)
            {
                foreach (var inner in Runs(span.Inlines))
                {
                    yield return inner;
                }
            }
        }
    }

    private static void AssertTokens()
    {
        Expect(MaddenArabic, "RPCS3", "#12514", "RPCS3", "v0.0.21-13399");
        Expect(Patterns[0], "RPCS3 0.0.41-19440");
        Expect(Patterns[1], "0.0.17-12652");
        Expect(Patterns[2], "1.09");
        Expect(Patterns[3], "dev_hdd0/game/BLUS30375/USRDIR");
        Expect(Patterns[4], "BLUS31059\\PS3_GAME\\USRDIR\\autoexec.cfg");
        Expect(Patterns[5], ".../rpcs3/dev_hdd0/music/");
        Expect(Patterns[6], "gosredirector.ea.com==95.217.209.57");
        Expect(Patterns[7], "185.194.142.4");
        Expect(Patterns[8], "NPEA00374/NPUA80756");
        Expect(Patterns[9], "Resolution Scale", "200%", "+200%", "25-35");
        Expect(Patterns[10], "mod_v2_by_lipsum.7z");
        Expect(Patterns[11], "PR #18644", "#12514");
        Expect(Patterns[12], "RPCS3", "L2/R2");
        foreach (var (_, text, english) in SourceLinks)
        {
            Expect(text, english);
        }

        foreach (var (text, english) in Sentences)
        {
            Expect(text, english);
        }

        // A whole English segment, without digits too; Arabic punctuation and the
        // Arabic sentence's period stay outside it.
        Expect("اضبط PPU Decoder على Recompiler (LLVM).", "PPU Decoder", "Recompiler (LLVM)");
        Expect("هل جربت Vulkan؟ ثم RPCS3؛ بعده OpenGL، وأخيرًا", "Vulkan", "RPCS3", "OpenGL");
        // A segment stops at an Arabic letter and at a line break.
        Expect("BLUS30375التي", "BLUS30375");
        Expect("شغّل RPCS3\nv0.0.21-13399 ثم", "RPCS3", "v0.0.21-13399");
        // Arabic-Indic digits, and brackets with nothing English inside.
        Expect("بمعدل ٣٠ إطارًا.");
        Expect("(انظر) - [عربي]");
    }

    private static void Expect(string text, params string[] expected)
    {
        var actual = LinkText.LtrSegments(text).Select(segment => text.Substring(segment.Start, segment.Length)).ToArray();
        Assert(actual.SequenceEqual(expected),
            $"LTR segments of '{text}': expected [{string.Join(" | ", expected)}], got [{string.Join(" | ", actual)}].");
    }

    private static void AssertRendered()
    {
        var language = UiText.CurLang;
        try
        {
            NoteSvc.SetSyncedWikiArabic(null);
            UiText.SetLang(UiText.ArabLang);
            var madden = MaddenNote();
            var arabic = NoteSvc.Trans(madden.Text).Body;
            Assert(arabic == MaddenArabic, $"The reviewed Arabic Madden NFL 10 note changed: {arabic}");

            // The real example, as the Wiki notes card and the cat.json ToolTip show it.
            foreach (var interactive in new[] { true, false })
            {
                var block = Arabic(arabic, interactive);
                Assert(Reversed(block).Count == 0,
                    $"Interactive={interactive}: technical tokens read reversed in the Arabic Madden NFL 10 note: {string.Join(" | ", Reversed(block))}");
                AssertSameText(block, arabic);
            }

            foreach (var pattern in Patterns)
            {
                var block = Arabic(pattern, true);
                Assert(Reversed(block).Count == 0,
                    $"Technical tokens read reversed in '{pattern}': {string.Join(" | ", Reversed(block))}");
                AssertSameText(block, pattern);
            }

            // A [url label] keeps a Hyperlink whose label tokens read LTR.
            const string Linked = "ثبّت [https://www.7-zip.org/ 7-Zip] ثم راجع [https://github.com/RPCS3/rpcs3/pull/18644 #18644].";
            var linked = Arabic(Linked, true);
            var links = linked.Inlines.OfType<Hyperlink>().ToArray();
            Assert(links.Length == 2 &&
                   links.Select(link => string.Concat(link.Inlines.OfType<Run>().Select(run => run.Text))).SequenceEqual(["7-Zip", "#18644"]) &&
                   links.All(link => link.NavigateUri is not null),
                "A labelled link must stay one Hyperlink with its exact label.");
            Assert(Reversed(linked).Count == 0, $"A link label token reads reversed: {string.Join(" | ", Reversed(linked))}");

            // A segment never crosses a Hyperlink: the label and the text after it
            // are separate segments.
            const string Bounded = "راجع [https://github.com/RPCS3/rpcs3/pull/18644 PR #18644] (#12514) ثم أعد التشغيل.";
            var bounded = Arabic(Bounded, true);
            var label = bounded.Inlines.OfType<Hyperlink>().Single().Inlines.OfType<Run>().ToArray();
            Assert(label is [{ Text: "PR #18644", FlowDirection: FlowDirection.LeftToRight }] &&
                   bounded.Inlines.OfType<Run>().Any(run => run is { Text: "(#12514)", FlowDirection: FlowDirection.LeftToRight }) &&
                   Reversed(bounded).Count == 0,
                $"A segment must stop at the Hyperlink: {string.Join(" | ", Runs(bounded.Inlines).Select(run => run.Text))}");

            // English stays one plain Run with no local direction or language.
            UiText.SetLang(UiText.EnLang);
            var english = new TextBlock();
            LinkText.SetText(english, NoteSvc.Trans(madden.Text).Body);
            var run = english.Inlines.OfType<Run>().SingleOrDefault();
            Assert(english.Inlines.Count == 1 && run is not null && run.Text == MaddenEnglish &&
                   run.ReadLocalValue(Inline.FlowDirectionProperty) == DependencyProperty.UnsetValue &&
                   run.ReadLocalValue(FrameworkContentElement.LanguageProperty) == DependencyProperty.UnsetValue,
                "The English note must stay one plain Run, unchanged.");
        }
        finally
        {
            UiText.SetLang(language);
        }
    }

    // Every shipped Arabic Wiki note: each whole English segment, found without
    // LinkText, reads in its own order.
    private static void AssertShipped()
    {
        var language = UiText.CurLang;
        try
        {
            NoteSvc.SetSyncedWikiArabic(null);
            UiText.SetLang(UiText.ArabLang);
            var outputs = NoteRevealT.Shipped().Pages.SelectMany(page => page.Notes)
                .Select(note => NoteSvc.Trans(note.Text).Body)
                .Where(NoteSvc.ContainsArabic)
                .ToArray();
            var withSegments = 0;
            var segments = 0;
            var multiWord = 0;
            var withoutBrackets = 0;
            var reversed = new List<string>();
            foreach (var output in outputs)
            {
                var block = Arabic(output, true);
                var found = Segments(block);
                if (found.Count == 0)
                {
                    continue;
                }

                withSegments++;
                segments += found.Count;
                multiWord += found.Count(item => item.Text.Contains(' ', StringComparison.Ordinal));
                // In order with its brackets means in order without them too.
                var bad = Reversed(block);
                reversed.AddRange(bad);
                withoutBrackets += bad.Count == 0 ? 0 : Reversed(block, ignoreBrackets: true).Count;
            }

            Console.WriteLine($"NoteBidi arabic={outputs.Length} withEnglish={withSegments} segments={segments} multiWord={multiWord} " +
                              $"reversed={reversed.Count} reversedIgnoringBrackets={withoutBrackets}");
            Assert(withSegments > 400 && reversed.Count == 0,
                $"{reversed.Count} English segments read out of order across {withSegments} shipped Arabic notes: {string.Join(" | ", reversed.Take(20))}");
        }
        finally
        {
            UiText.SetLang(language);
        }
    }

    internal static WikiFreeTextNote MaddenNote() =>
        NoteRevealT.Shipped().Pages.Single(page => page.Page == "Madden NFL 10").Notes
            .First(note => note.Text == MaddenEnglish);

    // As Rec.xaml shows a note, and Rtm.xaml a source link (Target), in Arabic:
    // RTL, ar-SA, European digits (Type.Body).
    private static TextBlock Arabic(string text, bool interactive, string? target = null)
    {
        var block = new TextBlock
        {
            FlowDirection = FlowDirection.RightToLeft,
            Language = XmlLanguage.GetLanguage(UiText.ArabLang),
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 14
        };
        NumberSubstitution.SetSubstitution(block, NumberSubstitutionMethod.European);
        LinkText.SetInteractive(block, interactive);
        LinkText.SetTarget(block, target);
        LinkText.SetText(block, text);
        block.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        block.Arrange(new Rect(block.DesiredSize));
        block.UpdateLayout();
        return block;
    }

    // Nothing is inserted: what the block holds and what a screen reader reads
    // is the bound text, character for character, with no format character.
    private static void AssertSameText(TextBlock block, string text)
    {
        var shown = Shown(block);
        var name = new TextBlockAutomationPeer(block).GetName();
        Assert(shown == text && name == text &&
               !shown.Any(ch => CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.Format),
            $"The rendered text must equal the source with no inserted character. Shown: {shown} Name: {name}");
    }

    // Whole English segments of the rendered text whose characters do not run
    // left to right on screen. WPF hit-testing names the character drawn at each
    // x around the segment (caret rectangles are not used: on a bidi boundary
    // they report the other side). Characters on different lines are not
    // compared. ignoreBrackets only splits a measurement: a bracket drawn in
    // the RTL paragraph is mirrored, so "(Linux)" around a whole segment can
    // look right while its brackets are out of order.
    internal static IReadOnlyList<string> Reversed(TextBlock block, bool ignoreBrackets = false)
    {
        var (text, positions, _) = Characters(block);
        var mirror = block.FlowDirection == FlowDirection.RightToLeft ? -1 : 1;
        var character = new Dictionary<int, int>();
        for (var index = 0; index < positions.Count; index++)
        {
            character[block.ContentStart.GetOffsetToPosition(positions[index])] = index;
        }

        var bad = new List<string>();
        foreach (var (start, length, _) in Segments(block))
        {
            var edges = Enumerable.Range(start, length)
                .SelectMany(index => new[]
                {
                    positions[index].GetCharacterRect(LogicalDirection.Forward),
                    positions[index].GetPositionAtOffset(1, LogicalDirection.Backward)!.GetCharacterRect(LogicalDirection.Backward)
                })
                .ToArray();
            var left = Math.Max(0, edges.Min(edge => edge.X) - 4);
            var right = Math.Min(block.ActualWidth, edges.Max(edge => edge.X) + 4);
            var first = new Dictionary<int, (double Line, double X)>();
            foreach (var line in edges.Select(edge => Math.Round(edge.Y + edge.Height / 2)).Distinct())
            {
                for (var x = left; x <= right; x += 1)
                {
                    var hit = block.GetPositionFromPoint(new Point(x, line), false);
                    if (hit is null)
                    {
                        continue;
                    }

                    var offset = block.ContentStart.GetOffsetToPosition(hit);
                    var drawn = hit.LogicalDirection == LogicalDirection.Forward ? offset : offset - 1;
                    if (character.TryGetValue(drawn, out var index) && index >= start && index < start + length &&
                        (!first.TryGetValue(index, out var seen) || (seen.Line == line && mirror * x < seen.X)))
                    {
                        first[index] = (line, mirror * x);
                    }
                }
            }

            // A space where a wrapped segment breaks is not drawn; every other
            // character must be.
            if (Enumerable.Range(start, length).Any(index => !first.ContainsKey(index) && !char.IsWhiteSpace(text[index])))
            {
                bad.Add($"{text.Substring(start, length)} (not hit)");
                continue;
            }

            var compared = Enumerable.Range(start, length)
                .Where(first.ContainsKey)
                .Where(index => !ignoreBrackets || !"()[]{}".Contains(text[index], StringComparison.Ordinal))
                .ToArray();
            for (var at = 1; at < compared.Length; at++)
            {
                var previous = first[compared[at - 1]];
                var current = first[compared[at]];
                if (previous.Line == current.Line && current.X <= previous.X)
                {
                    bad.Add(text.Substring(start, length));
                    break;
                }
            }
        }

        return bad;
    }

    internal static string Shown(TextBlock block) => Characters(block).Text;

    // The English segments of the rendered text, found without LinkText: a
    // stretch with no Arabic character, line break or guillemet, inside one
    // Hyperlink or outside all of them, trimmed of spaces, of its closing
    // sentence punctuation and of brackets or quotes it does not close. It
    // counts when it has a Latin letter or an ASCII digit.
    internal static IReadOnlyList<(int Start, int Length, string Text)> Segments(TextBlock block)
    {
        var (text, _, groups) = Characters(block);
        var found = new List<(int Start, int Length, string Text)>();
        var index = 0;
        while (index < text.Length)
        {
            if (Breaks(text[index]))
            {
                index++;
                continue;
            }

            var start = index;
            while (index < text.Length && !Breaks(text[index]) && groups[index] == groups[start])
            {
                index++;
            }

            var end = index;
            TrimSegment(text, ref start, ref end);
            if (end > start && text[start..end].Any(ch => char.IsAsciiLetterOrDigit(ch) || ch is >= 'À' and <= 'ɏ'))
            {
                found.Add((start, end - start, text[start..end]));
            }
        }

        return found;
    }

    private static bool Breaks(char ch) =>
        ch is '\r' or '\n' or '«' or '»' || NoteSvc.ContainsArabic(ch.ToString());

    private static void TrimSegment(string text, ref int start, ref int end)
    {
        while (end > start)
        {
            if (char.IsWhiteSpace(text[start]) || ",;:!?)]}".Contains(text[start], StringComparison.Ordinal))
            {
                start++;
            }
            else if (char.IsWhiteSpace(text[end - 1]) || ".,;:!?([{".Contains(text[end - 1], StringComparison.Ordinal))
            {
                end--;
            }
            else if (!Closed(text, start, end, start))
            {
                start++;
            }
            else if (!Closed(text, start, end, end - 1))
            {
                end--;
            }
            else
            {
                return;
            }
        }
    }

    // Whether a bracket or quote at an edge of text[start..end] has its partner
    // inside it. Any other character counts as closed.
    private static bool Closed(string text, int start, int end, int at)
    {
        var ch = text[at];
        if (ch is '"' or '\'')
        {
            return text[start..end].Count(item => item == ch) % 2 == 0;
        }

        const string Open = "([{";
        const string Close = ")]}";
        if (!Open.Contains(ch, StringComparison.Ordinal) && !Close.Contains(ch, StringComparison.Ordinal))
        {
            return true;
        }

        var stack = new Stack<int>();
        for (var index = start; index < end; index++)
        {
            if (Open.Contains(text[index], StringComparison.Ordinal))
            {
                stack.Push(index);
            }
            else if (Close.Contains(text[index], StringComparison.Ordinal) && stack.Count > 0 &&
                     Open[Close.IndexOf(text[index], StringComparison.Ordinal)] == text[stack.Peek()])
            {
                var partner = stack.Pop();
                if (partner == at || index == at)
                {
                    return true;
                }
            }
        }

        return false;
    }

    // Each text character of the block with the position just before it and
    // its group: the group changes at every Hyperlink edge, line break, button
    // or revealed block, none of which is text.
    private static (string Text, IReadOnlyList<TextPointer> Positions, IReadOnlyList<int> Groups) Characters(TextBlock block)
    {
        var text = new StringBuilder();
        var positions = new List<TextPointer>();
        var groups = new List<int>();
        var group = 0;
        var pointer = block.ContentStart;
        while (pointer is not null && pointer.CompareTo(block.ContentEnd) < 0)
        {
            var context = pointer.GetPointerContext(LogicalDirection.Forward);
            if (context != TextPointerContext.Text)
            {
                if (context == TextPointerContext.EmbeddedElement ||
                    (context is TextPointerContext.ElementStart or TextPointerContext.ElementEnd &&
                     pointer.GetAdjacentElement(LogicalDirection.Forward) is not System.Windows.Documents.Run))
                {
                    group++;
                }

                pointer = pointer.GetNextContextPosition(LogicalDirection.Forward);
                continue;
            }

            var run = pointer.GetTextInRun(LogicalDirection.Forward);
            for (var offset = 0; offset < run.Length; offset++)
            {
                positions.Add(pointer.GetPositionAtOffset(offset, LogicalDirection.Forward)!);
                groups.Add(group);
                text.Append(run[offset]);
            }

            pointer = pointer.GetPositionAtOffset(run.Length, LogicalDirection.Forward);
        }

        return (text.ToString(), positions, groups);
    }

    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception error)
            {
                failure = error;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        // Hang guard: hit-testing every English segment of the shipped notes
        // takes about 90 seconds.
        Assert(thread.Join(TimeSpan.FromSeconds(300)), "Note bidi render test timed out.");
        if (failure is not null)
        {
            throw new InvalidOperationException("Note bidi render failed.", failure);
        }
    }
}
