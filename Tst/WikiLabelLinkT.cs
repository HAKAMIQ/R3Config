using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Documents;
using R3Config.Ctrl;
using R3Config.Lang;
using R3Config.Svc;
using static R3Config.Tests.TestAssert;

namespace R3Config.Tests;

// Round-7: Wiki external links written as [url label] render only the label as a
// clickable link (url hidden, shown in the ToolTip); bare URLs stay as today but
// are isolated LTR inside Arabic text. http/https only; broken forms stay text.
internal static class WikiLabelLinkT
{
    private const string Lrm = "\u200E";
    private const string ModUrl = "https://www.nexusmods.com/demonssouls/mods/8";
    private const string DemonSource =
        "When doing resolution scaling in this game, gray outlines appear in the hud while in-game and in certain UI elements. You can use [https://www.nexusmods.com/demonssouls/mods/8 this mod] to eliminate that. Be sure to read the installation instruction in the description tab of the linked page carefully when applying this mod";

    public static void Run()
    {
        AssertSplit();
        AssertBrokenForms();
        AssertTranslatedNote();
        RunSta(AssertRendered);
    }

    private static void AssertSplit()
    {
        var english = WikiLinks.Split("Use [" + ModUrl + " this mod] to fix it.");
        Assert(english.Count == 3 &&
               english[0] is { Text: "Use ", Url: null } &&
               english[1].Text == "this mod" && english[1].Url == ModUrl &&
               english[2] is { Text: " to fix it.", Url: null },
            "[url label] must split into text, a label link carrying the url, and text.");

        var arabic = WikiLinks.Split("استخدم [" + ModUrl + " هذا المود] للإصلاح.");
        Assert(arabic.Count == 3 && arabic[1].Text == "هذا المود" && arabic[1].Url == ModUrl,
            "An Arabic label inside [url label] must be shown exactly as written.");

        var visible = string.Concat(arabic.Select(part => part.Text));
        Assert(!visible.Contains('[') && !visible.Contains(']') && !visible.Contains("https", StringComparison.Ordinal),
            "No bracket or url may remain in the visible text of a labelled link.");

        var two = WikiLinks.Split("[https://a.example/x one] and [http://b.example/y two]");
        Assert(two.Where(part => part.Url is not null).Select(part => part.Text + "=" + part.Url)
                   .SequenceEqual(["one=https://a.example/x", "two=http://b.example/y"]),
            "Several labelled links in one note must each keep their own url.");

        var bare = WikiLinks.Split("See https://forums.rpcs3.net/thread?id=1 now");
        Assert(bare.Single(part => part.Url is not null).Text == "https://forums.rpcs3.net/thread?id=1",
            "A bare URL keeps showing its address as the link text.");
    }

    private static void AssertBrokenForms()
    {
        foreach (var broken in new[]
                 {
                     "[ftp://x.example/a label] text",
                     "[javascript:alert(1) label] text",
                     "[file:///C:/Windows/notepad.exe label] text",
                     "[https:///nohost label] text",
                     "[//x.example label] text",
                     "[label only] text"
                 })
        {
            var parts = WikiLinks.Split(broken);
            Assert(parts.All(part => part.Url is null) &&
                   string.Concat(parts.Select(part => part.Text)) == broken,
                $"'{broken}' must stay plain text with no link.");
        }

        var unclosed = WikiLinks.Split("[https://x.example/a unfinished label");
        Assert(!unclosed.Any(part => part.Url is not null && part.Text == "unfinished label") &&
               string.Concat(unclosed.Select(part => part.Text)) == "[https://x.example/a unfinished label",
            "An unclosed [url label] must not become a labelled link and must lose no character.");

        var noLabel = WikiLinks.Split("[https://x.example/a] text");
        Assert(noLabel.All(part => part.Url is null || part.Text == part.Url) &&
               string.Concat(noLabel.Select(part => part.Text)) == "[https://x.example/a] text",
            "[url] without a label is not a labelled link.");
    }

    private static void AssertTranslatedNote()
    {
        var original = UiText.CurLang;
        try
        {
            NoteSvc.SetSyncedWikiArabic(null);
            foreach (var language in new[] { UiText.ArabLang, UiText.EnLang })
            {
                UiText.SetLang(language);
                var body = NoteSvc.Trans(DemonSource).Body;
                var parts = WikiLinks.Split(body);
                var link = parts.SingleOrDefault(part => part.Url == ModUrl);
                var visible = string.Concat(parts.Select(part => part.Text));
                Assert(link is not null &&
                       !visible.Contains('[') && !visible.Contains(']') &&
                       !visible.Contains("nexusmods", StringComparison.Ordinal),
                    $"[{language}] The Demon's Souls mod link must show only its label. Visible: {visible}");
            }
        }
        finally
        {
            NoteSvc.SetSyncedWikiArabic(null);
            UiText.SetLang(original);
        }
    }

    private static void AssertRendered()
    {
        var block = new TextBlock();
        LinkText.SetText(block, "استخدم [" + ModUrl + " هذا المود] للإصلاح.");
        var link = block.Inlines.OfType<Hyperlink>().Single();
        Assert(link.NavigateUri?.AbsoluteUri == ModUrl &&
               Equals(link.ToolTip, ModUrl) &&
               string.Concat(link.Inlines.OfType<Run>().Select(run => run.Text)) == "هذا المود",
            "A labelled link must show its label, navigate to the url and carry the url in its ToolTip.");
        var visible = Visible(block);
        Assert(visible == "استخدم هذا المود للإصلاح.",
            $"The rendered text must contain no bracket or url. Rendered: {visible}");

        var english = new TextBlock();
        LinkText.SetText(english, "Use [" + ModUrl + " this mod] now");
        Assert(Visible(english) == "Use this mod now" &&
               english.Inlines.OfType<Hyperlink>().Single().NavigateUri?.AbsoluteUri == ModUrl,
            "English labelled link must render 'Use this mod now' with the url only on the link.");

        var broken = new TextBlock();
        LinkText.SetText(broken, "[ftp://x.example/a label] text");
        Assert(!broken.Inlines.OfType<Hyperlink>().Any() && Visible(broken) == "[ftp://x.example/a label] text",
            "A broken or non-http form must render as plain text.");

        var bare = new TextBlock();
        LinkText.SetText(bare, "شاهد https://forums.rpcs3.net/thread?id=1 الآن");
        var inlines = bare.Inlines.ToList();
        var index = inlines.FindIndex(inline => inline is Hyperlink);
        Assert(index > 0 && index < inlines.Count - 1 &&
               inlines[index - 1] is Run { Text: { } before } && before.EndsWith(Lrm, StringComparison.Ordinal) &&
               inlines[index + 1] is Run { Text: { } after } && after.StartsWith(Lrm, StringComparison.Ordinal) &&
               ((Hyperlink)inlines[index]).NavigateUri?.AbsoluteUri == "https://forums.rpcs3.net/thread?id=1",
            "A bare URL inside Arabic text must keep its address but be isolated LTR with marks on both sides.");
    }

    private static string Visible(TextBlock block)
    {
        var builder = new StringBuilder();
        foreach (var run in block.Inlines.SelectMany(Runs))
        {
            builder.Append(run.Text);
        }

        return builder.ToString().Replace(Lrm, string.Empty, StringComparison.Ordinal);
    }

    private static IEnumerable<Run> Runs(Inline inline) =>
        inline switch
        {
            Run run => [run],
            Span span => span.Inlines.SelectMany(Runs),
            _ => []
        };

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
        Assert(thread.Join(TimeSpan.FromSeconds(30)), "Wiki label link render test timed out.");
        if (failure is not null)
        {
            throw new InvalidOperationException("Wiki label link render failed.", failure);
        }
    }
}
