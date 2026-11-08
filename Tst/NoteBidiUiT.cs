using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using R3Config.Ctrl;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;
using R3Config.Vm;

namespace R3Config.Tests;

// Round 18 runtime render inside UiGate (both languages, both themes): the
// Madden NFL 10 Wiki note in the real Game Settings view. In Arabic every
// technical token reads left to right (v0.0.21-13399, never 13399-v0.0.21) and
// the note's text and automation name are the reviewed translation exactly; in
// English the note is one plain Run.
internal static class NoteBidiUiT
{
    public static void VerifyRuntime(string language, bool isDark, string? shotDirectory)
    {
        var root = Path.Combine(Path.GetTempPath(), $"R3Test-NoteBidiUi-{Guid.NewGuid():N}");
        var model = new GameSettingsModel(
            "BLUS30375",
            "Madden NFL 10",
            [],
            [new GameNote(NoteBidiT.MaddenNote(), WikiNoteApplicability.General)],
            []);

        var plan = new PlanVm();
        plan.Load(DbConfigLayerT.FixturePlan(root), model);
        plan.ClearSel();

        var view = new RecView { DataContext = new Context(plan) };
        var window = new Window
        {
            Content = view,
            Width = 560,
            Height = 700,
            ShowActivated = false,
            ShowInTaskbar = false
        };
        window.Show();
        Drain(window.Dispatcher);
        try
        {
            var notes = Descendants<Border>(view).Single(item => AutomationProperties.GetAutomationId(item) == "Settings.Notes");
            var expected = language == UiText.ArabLang ? NoteBidiT.MaddenArabic : NoteBidiT.MaddenEnglish;
            var shown = Descendants<TextBlock>(notes)
                .Where(block => block.IsVisible && NoteBidiT.Shown(block) == expected)
                .ToArray();
            Check(shown.Length == 1, $"{language}: the Madden NFL 10 note must render once ({shown.Length}).");
            var note = shown[0];

            var reversed = NoteBidiT.Reversed(note);
            Check(reversed.Count == 0,
                $"{language} dark={isDark}: technical tokens read reversed in the Madden NFL 10 note: {string.Join(" | ", reversed)}");
            var name = UIElementAutomationPeer.CreatePeerForElement(note)?.GetName();
            Check(name == expected, $"{language}: the note's automation name must be the note text exactly. Name: {name}");
            if (language == UiText.ArabLang)
            {
                Check(note.FlowDirection == FlowDirection.RightToLeft,
                    $"{language}: the Arabic note paragraph must stay RTL.");
                var token = note.Inlines.OfType<Run>().SingleOrDefault(run => run.Text == "v0.0.21-13399");
                Check(token is not null && token.FlowDirection == FlowDirection.LeftToRight,
                    $"{language}: v0.0.21-13399 must be its own LTR Run.");
            }
            else
            {
                Check(note.Inlines.Count == 1 && note.Inlines.FirstInline is Run { Text: NoteBidiT.MaddenEnglish } run &&
                      run.ReadLocalValue(Inline.FlowDirectionProperty) == DependencyProperty.UnsetValue,
                    $"{language}: the English note must stay one plain Run.");
            }

            Console.WriteLine($"NoteBidiUi {language} dark={isDark} note={note.ActualWidth:0}x{note.ActualHeight:0} runs={note.Inlines.Count}");
            if (!string.IsNullOrWhiteSpace(shotDirectory))
            {
                Directory.CreateDirectory(shotDirectory);
                var bitmap = new RenderTargetBitmap(
                    (int)Math.Ceiling(view.ActualWidth), (int)Math.Ceiling(view.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(view);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var stream = File.Create(Path.Combine(
                    shotDirectory, $"{(isDark ? "dark" : "light")}-{language}-NoteBidi.png"));
                encoder.Save(stream);
            }
        }
        finally
        {
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
