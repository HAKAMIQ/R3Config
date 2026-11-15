using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using R3Config.Application;
using R3Config.Composition;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;
using R3Config.Vm;

namespace R3Config.Tests;

// Keyboard use of the main tab strip on the real MainWin, in ar-SA and en-US.
// Key presses go through WPF's InputManager, so KeyboardNavigation handles
// them as it handles a physical key and keyboard is the most recent input
// device (the condition for a focus ring). From a selected main tab header,
// Tab must enter that tab's content without selecting another tab; the arrow
// keys move between the headers; and the stops reached by Tab (the Runtime
// inner tab header and the first Runtime button) carry a visible focus ring
// (a FocusVisualStyle adorner that draws a stroke). Settings live under a
// temp root only.
internal static class MainTabKeyboardUiT
{
    private static readonly string[] MainTabs = ["Settings.Tab", "Patches.Tab", "Canary.Tab", "Artemis.Tab", "Runtime.Tab"];

    public static void Render()
    {
        var originalLanguage = UiText.CurLang;
        var context = new TestContext();
        var root = context.CreateTempRoot("R3ConfigMainTabKeyboard");
        try
        {
            foreach (var language in new[] { UiText.ArabLang, UiText.EnLang })
            {
                UiText.SetLang(language);
                UiLaySvc.Apply();
                Verify(language, Path.Combine(root, language, "settings.json"));
            }
        }
        finally
        {
            UiText.SetLang(originalLanguage);
            UiLaySvc.Apply();
            context.Cleanup();
        }
    }

    private static void Verify(string language, string settingsPath)
    {
        var vm = new MainVm(
            new AppRuntimeServices(new AppFlow(AppComposition.Build(new Rpcs3MutationGuard(() => false))), new SetSvc(settingsPath)),
            isRpcs3Running: () => false,
            confirm: (_, _) => true,
            info: (_, _) => { });
        var game = new Rpcs3GameDescriptor("BLUS90071", "Keyboard fixture", string.Empty, "01.00", null, null);
        var row = new GameVm(game, null, new RecSum(game.Serial, 0, 0, 0, 0, 0, 0, []), game.Serial, game.Title, isDetectedInRpcs3: true);
        vm.GameList.Load([row]);
        vm.GameList.SelGame = row;

        var main = new MainWin(vm) { Width = 1280, Height = 800 };
        try
        {
            main.Show();
            Activate(main);
            var tabs = main.FindName("Tabs") as TabControl ?? throw new InvalidDataException("Main tabs were not found.");
            Assert(tabs.IsVisible, $"{language}: the main tabs must be visible once a game is selected.");
            var headers = MainTabs.Select(id => All<TabItem>(tabs).Single(item => AutomationProperties.GetAutomationId(item) == id)).ToArray();
            var stops = new List<string>();

            // Tab from each selected header enters its content (or leaves the
            // strip when the content has no stop) and never selects another tab.
            foreach (var header in headers)
            {
                header.IsSelected = true;
                Settle(main);
                Assert(header.Focus() && header.IsKeyboardFocused,
                    $"TEST_ENV {language}: the {AutomationProperties.GetAutomationId(header)} header could not take keyboard focus (window not active).");
                Press(main, Key.Tab);
                var focused = Keyboard.FocusedElement as DependencyObject;
                var landed = Describe(focused);
                stops.Add($"{AutomationProperties.GetAutomationId(header)} -> {landed}");
                Assert(header.IsSelected && !headers.Any(other => ReferenceEquals(other, focused)),
                    $"{language}: Tab from the {AutomationProperties.GetAutomationId(header)} header must enter its content, not select or focus another tab. Landed on {landed}.");
                if (focused is FrameworkElement stop && IsInside(stop, tabs.SelectedContent as DependencyObject))
                {
                    AssertFocusRing(main, stop, language);
                }
            }

            // Runtime: Tab lands inside the selected content, on its inner tab header, with a focus ring.
            var runtime = headers[^1];
            runtime.IsSelected = true;
            Settle(main);
            runtime.Focus();
            Press(main, Key.Tab);
            var inner = Keyboard.FocusedElement as TabItem;
            Assert(inner is not null && IsInside(inner, tabs.SelectedContent as DependencyObject) &&
                   AutomationProperties.GetAutomationId(inner).StartsWith("Runtime.", StringComparison.Ordinal),
                $"{language}: Tab from the Runtime header must reach the Runtime inner tab header. Landed on {Describe(Keyboard.FocusedElement as DependencyObject)}.");
            AssertFocusRing(main, inner!, language);

            // Continue to the first enabled Button in the Runtime Overview content.
            Button? button = null;
            for (var press = 0; press < 12 && button is null; press++)
            {
                Press(main, Key.Tab);
                button = Keyboard.FocusedElement as Button;
            }

            Assert(button is not null,
                $"{language}: Tab from the Runtime inner header never reached a Button in the Runtime content.");
            AssertFocusRing(main, button!, language);

            // Arrow keys move between main headers and select them; the
            // opposite arrow returns. The strip follows the interface direction.
            headers[0].IsSelected = true;
            Settle(main);
            headers[0].Focus();
            Press(main, Key.Right);
            var afterRight = Keyboard.FocusedElement as TabItem;
            Assert(afterRight is not null && headers.Contains(afterRight) && !ReferenceEquals(afterRight, headers[0]) && afterRight.IsSelected,
                $"{language}: Right arrow on a main header must move to and select another header. Landed on {Describe(Keyboard.FocusedElement as DependencyObject)}.");
            Press(main, Key.Left);
            Assert(ReferenceEquals(Keyboard.FocusedElement, headers[0]) && headers[0].IsSelected,
                $"{language}: Left arrow must return to the first header. Landed on {Describe(Keyboard.FocusedElement as DependencyObject)}.");
            AssertFocusRing(main, headers[0], language);

            Console.WriteLine($"Main tab keyboard ({language}): {string.Join(" | ", stops)}; ring on {AutomationProperties.GetAutomationId(inner!)} and {Describe(button)}.");
        }
        finally
        {
            main.Close();
            vm.Dispose();
        }
    }

    // The ring WPF draws for keyboard focus: an adorner built from the
    // element's FocusVisualStyle whose shape has a visible stroke.
    private static void AssertFocusRing(Window window, FrameworkElement element, string language)
    {
        Settle(window);
        var layer = AdornerLayer.GetAdornerLayer(element);
        var adorners = layer?.GetAdorners(element) ?? [];
        var strokes = adorners.SelectMany(adorner => All<System.Windows.Shapes.Shape>(adorner))
            .Where(shape => shape.Stroke is SolidColorBrush { Color.A: > 0 } && shape.StrokeThickness > 0 && shape.ActualWidth > 0)
            .ToArray();
        Assert(element.IsKeyboardFocused && element.FocusVisualStyle is not null && strokes.Length > 0,
            $"{language}: keyboard focus on {Describe(element)} must show a focus ring (FocusVisualStyle={(element.FocusVisualStyle is null ? "null" : "set")}, " +
            $"adorner layer={(layer is null ? "none" : "found")}, adorners={adorners.Length}, stroked shapes={strokes.Length}, " +
            $"most recent input={InputManager.Current.MostRecentInputDevice?.GetType().Name ?? "none"}, keyboard cues={SystemParameters.KeyboardCues}).");
    }

    // A physical key arrives as a raw input report, which also makes the
    // keyboard the most recent input device; WPF draws focus rings only in
    // that state. Synthesized key events skip the raw report, so that state is
    // set here the same way before each press.
    private static readonly System.Reflection.PropertyInfo MostRecentInputDevice =
        typeof(InputManager).GetProperty(nameof(InputManager.MostRecentInputDevice))
        ?? throw new InvalidOperationException("TEST_ENV: InputManager.MostRecentInputDevice was not found.");

    private static void Press(Window window, Key key)
    {
        var source = PresentationSource.FromVisual(window) ?? throw new InvalidOperationException("The main window has no presentation source.");
        var setter = MostRecentInputDevice.GetSetMethod(nonPublic: true)
            ?? throw new InvalidOperationException("TEST_ENV: InputManager.MostRecentInputDevice has no setter.");
        setter.Invoke(InputManager.Current, [Keyboard.PrimaryDevice]);
        InputManager.Current.ProcessInput(new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
        InputManager.Current.ProcessInput(new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key) { RoutedEvent = Keyboard.PreviewKeyUpEvent });
        Settle(window);
    }

    private static void Activate(Window window)
    {
        WindowCapture.Foreground(new WindowInteropHelper(window).Handle);
        window.Activate();
        Settle(window);
    }

    private static void Settle(Window window)
    {
        for (var pass = 0; pass < 3; pass++)
        {
            window.UpdateLayout();
            window.Dispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));
        }
    }

    private static string Describe(DependencyObject? element) => element switch
    {
        null => "<nothing>",
        FrameworkElement fe => $"{fe.GetType().Name}[{AutomationProperties.GetAutomationId(fe)}]'{AutomationProperties.GetName(fe)}'",
        _ => element.GetType().Name
    };

    private static bool IsInside(DependencyObject element, DependencyObject? ancestor)
    {
        for (var node = element; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (ReferenceEquals(node, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    private static List<T> All<T>(DependencyObject root)
        where T : DependencyObject
    {
        var found = new List<T>();
        Walk(root);
        return found;

        void Walk(DependencyObject node)
        {
            if (node is T match)
            {
                found.Add(match);
            }

            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(node); index++)
            {
                Walk(VisualTreeHelper.GetChild(node, index));
            }
        }
    }
}
