using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using R3Config.Lang;
using R3Config.Svc;
using R3Config.Tests;
using R3Config.Vm;

// Keyboard and screen-reader access to the Runtime view (Ctrl/Rtm.xaml), in
// both languages. Screen readers read the UI Automation tree built from the
// automation peers, so the tree walked here is the one UIA serves: from the
// window peer through GetChildren. For each inner tab the selected content
// must be in that tree; on the Hardware tab the trusted PPU hash text and its
// Copy button must be exposed, named, keyboard focusable and invokable.
// Keyboard traversal uses the same navigation the Tab key runs
// (TraversalRequest Next/Previous) from the selected inner tab header, and the
// Copy button must answer the Enter key. Called by RuntimeViewUiT with the
// trusted snapshot loaded and RuntimeVm.CopyText replaced, so no test touches
// the real clipboard.
internal static class RuntimeA11yUiT
{
    private const string CopyId = "Runtime.PpuHash.Copy";
    private const int MaxTabPresses = 40;

    public static void Verify(Window window, R3Config.Ctrl.RuntimeView view, RuntimeVm vm, string hash, List<string> copied)
    {
        foreach (var language in new[] { UiText.EnLang, UiText.ArabLang })
        {
            UiText.SetLang(language);
            UiLaySvc.Apply();
            vm.SetLang();
            Settle(window);

            foreach (var (tab, contentId) in new[]
                     {
                         (LogDiagVm.TabOverview, "Runtime.Overview.Scroll"),
                         (LogDiagVm.TabHardware, "Runtime.Scroll"),
                         (LogDiagVm.TabLogs, "Runtime.Logs.Scroll"),
                         (LogDiagVm.TabEvidence, "Runtime.Evidence.Scroll")
                     })
            {
                vm.LogDiag.SelectedTab = tab;
                Settle(window);
                var ids = Tree(window).Select(peer => peer.GetAutomationId()).ToHashSet(StringComparer.Ordinal);
                Assert(ids.Contains(contentId),
                    $"Screen readers must reach the content of Runtime inner tab {tab} ({contentId}) under {language}; " +
                    $"the UI Automation tree exposes only: {string.Join(", ", ids.Where(id => id.StartsWith("Runtime.", StringComparison.Ordinal)).Order(StringComparer.Ordinal))}.");
            }

            vm.LogDiag.SelectedTab = LogDiagVm.TabHardware;
            Settle(window);
            var tree = Tree(window);
            var copyPeer = tree.SingleOrDefault(peer => peer.GetAutomationId() == CopyId);
            Assert(copyPeer is not null &&
                   copyPeer.GetAutomationControlType() == AutomationControlType.Button &&
                   copyPeer.GetName() == UiText.GetText("Link.Copy") &&
                   copyPeer.IsKeyboardFocusable() && copyPeer.IsEnabled() && !copyPeer.IsOffscreen(),
                $"The PPU hash Copy button must be in the UI Automation tree as an enabled, on-screen, focusable Button named '{UiText.GetText("Link.Copy")}' under {language}.");
            Assert(tree.Any(peer => peer.GetName() == hash && !peer.IsOffscreen()),
                $"Screen readers must read the trusted PPU hash text under {language}.");

            var before = copied.Count;
            ((IInvokeProvider)copyPeer!.GetPattern(PatternInterface.Invoke)).Invoke();
            Settle(window);
            Assert(copied.Count == before + 1 && copied[^1] == hash,
                $"Invoking the Copy button through UI Automation copies the PPU hash under {language}.");

            VerifyKeyboard(window, view, language, hash, copied);
        }
    }

    // From the selected inner tab header, Tab must reach the Copy button
    // without leaving the window; every stop is named; Shift+Tab returns to
    // the previous stop; Enter on the focused button copies the hash.
    private static void VerifyKeyboard(Window window, R3Config.Ctrl.RuntimeView view, string language, string hash, List<string> copied)
    {
        Activate(window);
        var header = All<TabItem>(view).Single(item => item.IsSelected &&
            System.Windows.Automation.AutomationProperties.GetAutomationId(item) == "Runtime.Hardware.Tab");
        Assert(header.Focus() && header.IsKeyboardFocused,
            $"TEST_ENV: the Hardware tab header could not take keyboard focus under {language} (window not active).");

        var stops = new List<string>();
        Button? copy = null;
        IInputElement? previous = null;
        for (var press = 0; press < MaxTabPresses && copy is null; press++)
        {
            previous = Keyboard.FocusedElement;
            ((UIElement)Keyboard.FocusedElement).MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            Settle(window);
            var focused = Keyboard.FocusedElement as FrameworkElement;
            Assert(focused is not null && IsInside(window, focused),
                $"Tab moved keyboard focus out of the window after {press + 1} presses under {language}. Stops: {string.Join(" | ", stops)}");
            var peer = UIElementAutomationPeer.CreatePeerForElement(focused!);
            var name = peer?.GetName() ?? string.Empty;
            var id = System.Windows.Automation.AutomationProperties.GetAutomationId(focused!);
            stops.Add($"{focused!.GetType().Name}:{id}:{name}");
            Assert(name.Length > 0,
                $"Tab stop {press + 1} ({focused.GetType().Name}, {id}) has no accessible name for a screen reader to speak under {language}. Stops: {string.Join(" | ", stops)}");
            if (focused is Button button && id == CopyId)
            {
                copy = button;
            }
        }

        Assert(copy is not null,
            $"Keyboard Tab from the Hardware tab header never reached the PPU hash Copy button in {MaxTabPresses} presses under {language}. " +
            $"Stops: {string.Join(" | ", stops)}");

        var before = copied.Count;
        var source = PresentationSource.FromVisual(copy!)!;
        // Enter clicks a focused Button directly; Space goes through mouse capture,
        // which real mouse activity on the test machine can cancel.
        copy!.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Enter) { RoutedEvent = Keyboard.KeyDownEvent });
        Settle(window);
        Assert(copied.Count == before + 1 && copied[^1] == hash,
            $"Enter on the focused Copy button copies the PPU hash under {language}.");

        copy.MoveFocus(new TraversalRequest(FocusNavigationDirection.Previous));
        Settle(window);
        Assert(ReferenceEquals(Keyboard.FocusedElement, previous),
            $"Shift+Tab from the Copy button returns to the previous stop under {language}.");
        Console.WriteLine($"Runtime keyboard ({language}): Copy reached after {stops.Count} Tab stops: {string.Join(" | ", stops)}");
    }

    // The UI Automation tree as UIA serves it: window peer, then GetChildren.
    private static List<AutomationPeer> Tree(Window window)
    {
        var found = new List<AutomationPeer>();
        var root = UIElementAutomationPeer.CreatePeerForElement(window)
                   ?? throw new InvalidOperationException("The test window has no automation peer.");
        Walk(root, 0);
        return found;

        void Walk(AutomationPeer peer, int depth)
        {
            if (depth > 80)
            {
                return;
            }

            found.Add(peer);
            peer.ResetChildrenCache();
            foreach (var child in peer.GetChildren() ?? [])
            {
                Walk(child, depth + 1);
            }
        }
    }

    private static bool IsInside(Window window, DependencyObject element)
    {
        for (DependencyObject? node = element; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (ReferenceEquals(node, window))
            {
                return true;
            }
        }

        return false;
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
