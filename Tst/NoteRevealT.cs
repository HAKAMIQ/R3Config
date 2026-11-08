using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using R3Config.Ctrl;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;
using static R3Config.Tests.TestAssert;

namespace R3Config.Tests;

// Round 12, display only: RPCS3 IP/Host Switches text (host=IPv4 joined by &&)
// never shows inline in a note; it sits behind "Show IP/Host" (LTR monospace
// block) and "Copy IP/Host" (the exact English source text). A trailing
// "Category:..." is dropped. A link is always a word, never a raw address, with
// "Show link" revealing the decoded address and "Copy"/"Open" beside it.
internal static class NoteRevealT
{
    internal const string DemonSwitches =
        "ds-eu-c.scej-online.jp=206.189.232.242&&ds-eu-g.scej-online.jp=206.189.232.242&&" +
        "c.demons-souls.com=206.189.232.242&&g.demons-souls.com=206.189.232.242&&" +
        "cmnap.scej-online.jp=206.189.232.242&&demons-souls.scej-online.jp=206.189.232.242";

    internal const string DemonWikiUrl = "https://wiki.rpcs3.net/index.php?title=Demon%27s_Souls";

    private const string Lrm = "‎";

    public static void Run()
    {
        AssertIpHostSplit();
        AssertShippedCounts();
        AssertLinkParts();
        RunSta(AssertRendered);
    }

    internal static WikiEvidenceDocument Shipped() =>
        WikiEvidenceCatalog.Load(Path.Combine(AppContext.BaseDirectory, "Data", WikiEvidenceCatalog.FileName)).Document;

    // The Demon's Souls RPCN note of the shipped Wiki snapshot.
    internal static WikiFreeTextNote DemonRpcnNote() =>
        Shipped().Pages.Single(page => page.Page == "Demon's Souls").Notes
            .Single(note => note.Class == WikiNoteClass.RpcnNote && note.Text.Contains("IP/Host", StringComparison.Ordinal));

    internal static string Visible(IEnumerable<NoteParts.Part> parts) =>
        string.Concat(parts.Where(part => part.Kind != NoteParts.PartKind.IpHost).Select(part => part.Text));

    private static void AssertIpHostSplit()
    {
        var demon = DemonRpcnNote();
        Assert(demon.Text.Contains(DemonSwitches, StringComparison.Ordinal),
            "The shipped Demon's Souls RPCN note must still carry the six IP/Host switches.");

        var english = NoteParts.Split(demon.Text);
        var visible = Visible(english);
        Assert(visible.TrimEnd() == "This title requires a private server to access all Netplay features. Enter the below text in the IP/Host Switches section:",
            $"The IP/Host switches and the trailing Category must leave the visible English text. Visible: {visible}");
        var switches = english.Where(part => part.Kind == NoteParts.PartKind.IpHost).ToArray();
        Assert(switches.Length == 1 && switches[0].Value == DemonSwitches &&
               !switches[0].Value!.Contains(Lrm, StringComparison.Ordinal) &&
               switches[0].Value!.IndexOfAny(['\r', '\n']) < 0,
            "The IP/Host part must carry the exact English source text with no LRM and no line break.");

        var original = UiText.CurLang;
        try
        {
            NoteSvc.SetSyncedWikiArabic(null);
            UiText.SetLang(UiText.ArabLang);
            var arabic = NoteSvc.Trans(demon.Text).Body;
            Assert(NoteSvc.ContainsArabic(arabic), "The Demon's Souls RPCN note must have its reviewed Arabic text.");
            var arabicParts = NoteParts.Split(arabic, demon.Text);
            var arabicVisible = Visible(arabicParts);
            Assert(NoteSvc.ContainsArabic(arabicVisible) &&
                   !arabicVisible.Contains("206.189", StringComparison.Ordinal) &&
                   !arabicVisible.Contains("Category", StringComparison.Ordinal),
                $"The Arabic note must hide the IP/Host text and the Category tail. Visible: {arabicVisible}");
            Assert(arabicParts.Single(part => part.Kind == NoteParts.PartKind.IpHost).Value == DemonSwitches,
                "Copying from the Arabic note must copy the exact English source switches.");

            const string ConfrontationSource =
                "185.194.142.4 Save Confrontation discord. https://discord.gg/CCKUy4ZS3J Category:Custom servers (Netplay)";
            var confrontation = NoteParts.Split(NoteSvc.Trans(ConfrontationSource).Body, ConfrontationSource);
            var confrontationVisible = Visible(confrontation);
            Assert(!confrontationVisible.Contains("الفئة:", StringComparison.Ordinal) &&
                   !confrontationVisible.Contains("Category", StringComparison.Ordinal) &&
                   confrontationVisible.Contains("185.194.142.4", StringComparison.Ordinal) &&
                   confrontation.All(part => part.Kind != NoteParts.PartKind.IpHost) &&
                   confrontation.Single(part => part.Kind == NoteParts.PartKind.BareLink).Value == "https://discord.gg/CCKUy4ZS3J",
                $"The translated Category tail goes when the source had one; a lone IP is not an IP/Host switch. Visible: {confrontationVisible}");
        }
        finally
        {
            NoteSvc.SetSyncedWikiArabic(null);
            UiText.SetLang(original);
        }

        var two = NoteParts.Split("Enter: a.example=1.2.3.4&&b-c.example.org=10.0.0.1. Done");
        Assert(Visible(two) == "Enter: . Done" &&
               two.Single(part => part.Kind == NoteParts.PartKind.IpHost).Value == "a.example=1.2.3.4&&b-c.example.org=10.0.0.1",
            "Several host=IPv4 entries joined by && are one switch list; a sentence period stays text.");

        foreach (var notSwitch in new[]
                 {
                     "185.194.142.4 Save Confrontation",
                     "set a.example=1.2.3.256 now",
                     "set a.example=1.2.3.4&& now",
                     "set a.example=01.2.3.4 now",
                     "set a.example=1.2.3.4.5 now",
                     "set host=1.2.3.4 now",
                     "see http://x.example/?a.b=1.2.3.4 now",
                     "a.example=1.2.3.4x"
                 })
        {
            Assert(NoteParts.Split(notSwitch).All(part => part.Kind != NoteParts.PartKind.IpHost) &&
                   NoteParts.IpHostSwitches(notSwitch).Count == 0,
                $"'{notSwitch}' is not a strict RPCS3 IP/Host switch list.");
        }

        Assert(Visible(NoteParts.Split("Line one Category:Custom servers (Netplay)\r\nLine two")) == "Line one\r\nLine two",
            "A Category tail is dropped at the end of its own line only.");
        var wikiCategoryUrl = "See https://wiki.rpcs3.net/index.php?title=Category:Custom_servers here";
        Assert(NoteParts.Split(wikiCategoryUrl).Single(part => part.Kind == NoteParts.PartKind.BareLink).Value ==
               "https://wiki.rpcs3.net/index.php?title=Category:Custom_servers",
            "A Category inside a URL is part of the URL, not a tail.");
    }

    private static void AssertShippedCounts()
    {
        var pages = Shipped().Pages;
        var notes = pages.SelectMany(page => page.Notes).Where(note => NoteParts.IpHostSwitches(note.Text).Count > 0).ToArray();
        var online = pages.SelectMany(page => page.Online).Where(item => NoteParts.IpHostSwitches(item.Notes).Count > 0).ToArray();
        var catalog = RecSvc.Load(Path.Combine(AppContext.BaseDirectory, "Data", "cat.json"));
        var catNotes = catalog.Rec.SelectMany(rec => rec.Changes).Count(change => NoteParts.IpHostSwitches(change.Notes).Count > 0);
        var categoryTails = pages.SelectMany(page => page.Notes).Count(note =>
            System.Text.RegularExpressions.Regex.IsMatch(note.Text, @"(?m)(?:^|[ \t])Category:[^\r\n]*$"));
        Console.WriteLine($"IP/Host notes: wiki={notes.Length} online={online.Length} cat.json={catNotes}; notes losing a Category tail={categoryTails}");
        Assert(notes.Length == 10 && online.Length == 1 && catNotes == 0,
            $"The shipped data must carry 10 Wiki notes and 1 RPCN online note with IP/Host switches and none in cat.json (got {notes.Length}/{online.Length}/{catNotes}).");

        var original = UiText.CurLang;
        try
        {
            NoteSvc.SetSyncedWikiArabic(null);
            UiText.SetLang(UiText.ArabLang);
            var translated = 0;
            foreach (var text in notes.Select(note => note.Text).Concat(online.Select(item => item.Notes!)))
            {
                var body = NoteSvc.Trans(text).Body;
                var parts = NoteParts.Split(body, text);
                var shown = Visible(parts);
                var exact = NoteParts.IpHostSwitches(text);
                var carries = exact.Any(item => body.Contains(item, StringComparison.Ordinal));
                translated += carries && NoteSvc.ContainsArabic(body) ? 1 : 0;
                Assert(parts.Where(part => part.Kind == NoteParts.PartKind.IpHost).Select(part => part.Value)
                           .SequenceEqual(carries ? exact : []) &&
                       NoteParts.IpHostSwitches(text).All(item => !shown.Contains(item, StringComparison.Ordinal)) &&
                       !shown.Contains("Category:", StringComparison.Ordinal),
                    $"Every shipped IP/Host note must hide the switches and copy them from the English source: {text}");
            }

            Console.WriteLine($"IP/Host notes with reviewed Arabic carrying the switches: {translated}");
        }
        finally
        {
            NoteSvc.SetSyncedWikiArabic(null);
            UiText.SetLang(original);
        }
    }

    private static void AssertLinkParts()
    {
        var bare = NoteParts.Split("See " + DemonWikiUrl + " now");
        Assert(bare.Single(part => part.Kind == NoteParts.PartKind.BareLink).Value == DemonWikiUrl &&
               NoteParts.IsWikiPage(DemonWikiUrl) &&
               !NoteParts.IsWikiPage("https://discord.gg/CCKUy4ZS3J") &&
               !NoteParts.IsWikiPage("https://wiki.rpcs3.net.evil.example/index.php") &&
               !NoteParts.IsWikiPage("javascript:alert(1)"),
            "A bare address is a link part; only wiki.rpcs3.net addresses are a Wiki page.");
        var labelled = NoteParts.Split("Use [https://www.nexusmods.com/demonssouls/mods/1 this mod] now");
        Assert(labelled.Single(part => part.Kind == NoteParts.PartKind.Link) is { Text: "this mod", Value: "https://www.nexusmods.com/demonssouls/mods/1" },
            "[url label] keeps its label and address.");
    }

    private static void AssertRendered()
    {
        var originalCopy = LinkText.CopyText;
        var originalOpen = LinkText.OpenUrl;
        var copied = new List<string>();
        var opened = new List<string>();
        LinkText.CopyText = copied.Add;
        LinkText.OpenUrl = url =>
        {
            opened.Add(url);
            return true;
        };
        var language = UiText.CurLang;
        try
        {
            foreach (var lang in new[] { UiText.ArabLang, UiText.EnLang })
            {
                UiText.SetLang(lang);
                copied.Clear();
                opened.Clear();

                var demon = DemonRpcnNote();
                var note = new TextBlock();
                LinkText.SetSource(note, demon.Text);
                LinkText.SetText(note, NoteSvc.Trans(demon.Text).Body);
                var visible = Visible(note);
                Assert(!visible.Contains("206.189", StringComparison.Ordinal) && !visible.Contains("Category", StringComparison.Ordinal),
                    $"[{lang}] the rendered note must not show the IP/Host text or the Category tail. Rendered: {visible}");
                var show = Button(note, "LinkText.IpHost.Show");
                var copy = Button(note, "LinkText.IpHost.Copy");
                Assert(Label(show) == UiText.GetText("IpHost.Show") && Label(copy) == UiText.GetText("IpHost.Copy") &&
                       Revealed(note) is null,
                    $"[{lang}] the IP/Host text must be replaced by Show and Copy buttons, hidden until asked.");
                Click(copy);
                Assert(copied.SequenceEqual([DemonSwitches]),
                    $"[{lang}] Copy IP/Host must copy the exact English source text with no LRM or line break.");
                Click(show);
                var block = Revealed(note);
                Assert(block is not null && block.FlowDirection == FlowDirection.LeftToRight &&
                       RevealedText(block) == DemonSwitches &&
                       RevealedFont(block).Contains("Mono", StringComparison.OrdinalIgnoreCase) &&
                       Label(Button(note, "LinkText.IpHost.Show")) == UiText.GetText("IpHost.Hide"),
                    $"[{lang}] Show IP/Host must reveal the exact text in an LTR monospace block.");
                Click(Button(note, "LinkText.IpHost.Show"));
                Assert(Revealed(note) is null, $"[{lang}] Hide must collapse the IP/Host block.");

                var source = new TextBlock();
                LinkText.SetText(source, DemonWikiUrl);
                LinkText.SetTarget(source, DemonWikiUrl);
                var wikiLink = source.Inlines.OfType<Hyperlink>().Single();
                Assert(Visible(source) == UiText.GetText("Link.WikiPage") && wikiLink.ToolTip is null &&
                       wikiLink.NavigateUri?.AbsoluteUri == DemonWikiUrl,
                    $"[{lang}] a raw Wiki address must render as the word '{UiText.GetText("Link.WikiPage")}' with no address anywhere. Rendered: {Visible(source)}");
                Assert(Label(Button(source, "LinkText.Link.Show")) == UiText.GetText("Link.Show") && Revealed(source) is null,
                    $"[{lang}] a link carries a small Show link button.");
                Click(Button(source, "LinkText.Link.Show"));
                var revealedUrl = Revealed(source);
                Assert(revealedUrl is not null && revealedUrl.FlowDirection == FlowDirection.LeftToRight &&
                       RevealedText(revealedUrl) == "https://wiki.rpcs3.net/index.php?title=Demon's_Souls",
                    $"[{lang}] Show link must reveal the decoded address LTR.");
                Click(Button(source, "LinkText.Link.Copy"));
                Click(Button(source, "LinkText.Link.Open"));
                Assert(copied.Last() == DemonWikiUrl && opened.SequenceEqual([DemonWikiUrl]),
                    $"[{lang}] Copy must copy and Open must open the original address.");

                var discord = new TextBlock();
                LinkText.SetText(discord, "Join https://discord.gg/CCKUy4ZS3J now");
                Assert(Visible(discord) == $"Join {UiText.GetText("Link.Generic")} now" &&
                       discord.Inlines.OfType<Hyperlink>().Single().ToolTip is null,
                    $"[{lang}] a bare non-Wiki address renders as the word '{UiText.GetText("Link.Generic")}'. Rendered: {Visible(discord)}");

                var unsafeText = new TextBlock();
                LinkText.SetText(unsafeText, "[ftp://x.example/a label] text");
                Assert(!Buttons(unsafeText).Any() && Visible(unsafeText) == "[ftp://x.example/a label] text",
                    $"[{lang}] a broken or non-http form stays plain text with no buttons.");
            }
        }
        finally
        {
            LinkText.CopyText = originalCopy;
            LinkText.OpenUrl = originalOpen;
            UiText.SetLang(language);
        }
    }

    internal static IEnumerable<Button> Buttons(TextBlock block) =>
        Containers(block.Inlines).Select(container => container.Child).SelectMany(Flatten).OfType<Button>();

    internal static Button Button(TextBlock block, string id) =>
        Buttons(block).FirstOrDefault(button => AutomationProperties.GetAutomationId(button) == id)
        ?? throw new InvalidOperationException($"Button '{id}' was not rendered.");

    internal static Border? Revealed(TextBlock block) =>
        Containers(block.Inlines).Select(container => container.Child).SelectMany(Flatten).OfType<Border>()
            .FirstOrDefault(border => AutomationProperties.GetAutomationId(border) == "LinkText.Revealed");

    internal static string RevealedText(Border block) =>
        Flatten(block).OfType<TextBlock>().Single(text => AutomationProperties.GetAutomationId(text) == "LinkText.RevealedText").Text;

    private static string RevealedFont(Border block) =>
        Flatten(block).OfType<TextBlock>().Single(text => AutomationProperties.GetAutomationId(text) == "LinkText.RevealedText").FontFamily.Source;

    internal static string Label(Button button) => button.Content as string ?? string.Empty;

    internal static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));

    // Visible words of the block: runs only; buttons and revealed blocks are UI.
    internal static string Visible(TextBlock block)
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

    private static IEnumerable<InlineUIContainer> Containers(InlineCollection inlines) =>
        inlines.SelectMany(inline => inline switch
        {
            InlineUIContainer container => [container],
            Span span => Containers(span.Inlines),
            _ => Enumerable.Empty<InlineUIContainer>()
        });

    private static IEnumerable<DependencyObject> Flatten(DependencyObject root)
    {
        yield return root;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            foreach (var nested in Flatten(child))
            {
                yield return nested;
            }
        }
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
        Assert(thread.Join(TimeSpan.FromSeconds(30)), "Note reveal render test timed out.");
        if (failure is not null)
        {
            throw new InvalidOperationException("Note reveal render failed.", failure);
        }
    }
}
