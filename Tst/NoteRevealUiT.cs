using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using R3Config.Ctrl;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Vm;

namespace R3Config.Tests;

// Round 12 runtime render inside UiGate (both languages, both themes): the Game
// Settings notes and RPCN cards show no IP/Host text, no raw address and no
// Category tail; the IP/Host and link buttons render, and a revealed value is
// an LTR monospace block inside its card.
internal static class NoteRevealUiT
{
    public static void VerifyRuntime(string language, bool isDark, string? shotDirectory)
    {
        var root = Path.Combine(Path.GetTempPath(), $"R3Test-NoteRevealUi-{Guid.NewGuid():N}");
        var pages = NoteRevealT.Shipped().Pages;
        var demonPage = pages.Single(page => page.Page == "Demon's Souls");
        var modNote = demonPage.Notes.First(note => note.Text.Contains("[https://www.nexusmods.com/demonssouls/mods/1 ", StringComparison.Ordinal));
        var topSpin = pages.Single(page => page.Page == "Top Spin 4").Online.Single();
        var model = new GameSettingsModel(
            "BLUS30442",
            "Demon's Souls",
            [],
            [
                new GameNote(NoteRevealT.DemonRpcnNote(), WikiNoteApplicability.General),
                new GameNote(modNote, WikiNoteApplicability.General)
            ],
            [topSpin],
            ApplicableOnline: [topSpin]);

        var plan = new PlanVm();
        plan.Load(DbConfigLayerT.FixturePlan(root), model);
        plan.ClearSel();

        var originalCopy = LinkText.CopyText;
        var copied = new List<string>();
        LinkText.CopyText = copied.Add;
        var view = new RecView { DataContext = new Context(plan) };
        var window = new Window
        {
            Content = view,
            Width = 560,
            Height = 900,
            ShowActivated = false,
            ShowInTaskbar = false
        };
        window.Show();
        Drain(window.Dispatcher);
        try
        {
            foreach (var expander in Descendants<Expander>(view))
            {
                expander.IsExpanded = true;
            }

            Drain(window.Dispatcher);
            var notes = Card(view, "Settings.Notes");
            var online = Card(view, "Settings.Online");
            Check(notes.Visibility == Visibility.Visible && online.Visibility == Visibility.Visible,
                $"{language}: the notes and RPCN cards must render.");

            var shown = string.Join("\n", Descendants<TextBlock>(notes).Concat(Descendants<TextBlock>(online))
                .Where(text => text.IsVisible)
                .Select(NoteRevealT.Visible));
            foreach (var forbidden in new[] { "206.189", "127.0.0.1", "http", "%27", "Category:", "nexusmods" })
            {
                Check(!shown.Contains(forbidden, StringComparison.Ordinal),
                    $"{language}: '{forbidden}' must not be visible in a note. Visible:\n{shown}");
            }

            var noteBlocks = Descendants<TextBlock>(notes).Concat(Descendants<TextBlock>(online)).ToArray();
            var ipShows = noteBlocks.SelectMany(NoteRevealT.Buttons)
                .Where(button => AutomationProperties.GetAutomationId(button) == "LinkText.IpHost.Show").ToArray();
            var ipCopies = noteBlocks.SelectMany(NoteRevealT.Buttons)
                .Where(button => AutomationProperties.GetAutomationId(button) == "LinkText.IpHost.Copy").ToArray();
            // Demon's Souls note (display + English original) and the Top Spin 4 RPCN notes.
            Check(ipShows.Length == 3 && ipCopies.Length == 3 && ipShows.All(button => button.IsVisible && button.ActualWidth > 0),
                $"{language}: every IP/Host occurrence must render Show and Copy buttons ({ipShows.Length}/{ipCopies.Length}).");

            NoteRevealT.Click(ipCopies[0]);
            Check(copied.SequenceEqual([NoteRevealT.DemonSwitches]),
                $"{language}: Copy IP/Host must copy the exact English source text.");

            var demonBlock = noteBlocks.First(block => NoteRevealT.Buttons(block).Contains(ipShows[0]));
            NoteRevealT.Click(ipShows[0]);
            Drain(window.Dispatcher);
            var ipBlock = NoteRevealT.Revealed(demonBlock);
            Check(ipBlock is not null && ipBlock.FlowDirection == FlowDirection.LeftToRight &&
                  NoteRevealT.RevealedText(ipBlock) == NoteRevealT.DemonSwitches,
                $"{language}: Show IP/Host must reveal the exact switches LTR.");
            Inside(ipBlock!, notes, $"{language}: the IP/Host block");

            var wikiLinks = Descendants<TextBlock>(notes).Where(block => NoteRevealT.Visible(block) == UiText.GetText("Link.WikiPage")).ToArray();
            Check(wikiLinks.Length == 2,
                $"{language}: each note's source must be the word '{UiText.GetText("Link.WikiPage")}' ({wikiLinks.Length}).");
            NoteRevealT.Click(NoteRevealT.Button(wikiLinks[0], "LinkText.Link.Show"));
            Drain(window.Dispatcher);
            var urlBlock = NoteRevealT.Revealed(wikiLinks[0]);
            Check(urlBlock is not null && NoteRevealT.RevealedText(urlBlock) == "https://wiki.rpcs3.net/index.php?title=Demon's_Souls",
                $"{language}: Show link must reveal the decoded Wiki address.");
            Inside(urlBlock!, notes, $"{language}: the revealed address");
            Console.WriteLine($"NoteRevealUi {language} dark={isDark} notes={notes.ActualWidth}x{notes.ActualHeight} ip={ipShows.Length}");

            if (!string.IsNullOrWhiteSpace(shotDirectory))
            {
                Directory.CreateDirectory(shotDirectory);
                var bitmap = new RenderTargetBitmap(
                    (int)Math.Ceiling(view.ActualWidth), (int)Math.Ceiling(view.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(view);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var stream = File.Create(Path.Combine(
                    shotDirectory, $"{(isDark ? "dark" : "light")}-{language}-NoteReveal.png"));
                encoder.Save(stream);
            }
        }
        finally
        {
            LinkText.CopyText = originalCopy;
            window.Close();
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    private static Border Card(DependencyObject root, string id) =>
        Descendants<Border>(root).Single(item => AutomationProperties.GetAutomationId(item) == id);

    private static void Inside(FrameworkElement element, FrameworkElement card, string what)
    {
        var box = element.TransformToAncestor(card).TransformBounds(new Rect(element.RenderSize));
        Check(element.ActualWidth > 0 && box.Left >= -1 && box.Right <= card.ActualWidth + 1,
            $"{what} must stay inside its card ({box.Left:0}..{box.Right:0} of {card.ActualWidth:0}).");
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var nested in Descendants<T>(child))
            {
                yield return nested;
            }
        }
    }

    private static void Drain(Dispatcher dispatcher)
    {
        var frame = new DispatcherFrame();
        dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    // The settings panel binds these two commands of MainVm; inert here.
    private sealed record Context(PlanVm Plan)
    {
        public System.Windows.Input.ICommand ApplyCmd { get; } = new Inert();

        public System.Windows.Input.ICommand DelSet { get; } = new Inert();
    }

    private sealed class Inert : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => false;

        public void Execute(object? parameter)
        {
        }
    }
}
