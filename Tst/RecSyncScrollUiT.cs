using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;
using R3Config.Vm;

namespace R3Config.Tests;

// Advanced -> Recommendation updates stacks a header, an expandable source
// table and the change table in one Grid whose last row takes the rest. The
// source table sits in an Auto row, so an uncapped one grows to its full
// content height, pushes the change table out of the window and leaves nothing
// to scroll. It must stay bounded and scroll inside itself instead.
internal static class RecSyncScrollUiT
{
    public static void Render()
    {
        var original = UiText.CurLang;
        Window? window = null;
        try
        {
            foreach (var lang in new[] { UiText.ArabLang, UiText.EnLang })
            {
                UiText.SetLang(lang);
                UiLaySvc.Apply();

                var vm = new RecSyncVm();
                for (var index = 0; index < 24; index++)
                {
                    vm.Sources.Add(new RecSyncSourceRowVm(
                        $"Source {index}", "OK", "2026-11-10", $"Detail of source {index}"));
                }

                for (var index = 0; index < 40; index++)
                {
                    vm.Changes.Add(new RecSyncChangeRowVm(Change(index)));
                }

                var view = new R3Config.View.ARecSyncView { DataContext = new Host(vm) };

                // The Advanced window's content area at its smallest supported
                // size, which is where the change table was lost.
                window = new Window { Content = view, Width = 760, Height = 560 };
                window.Show();
                Settle(window);

                var expander = All<Expander>(view).Single(item => AutomationProperties.GetAutomationId(item) == "Sync.AdvancedDetails");
                expander.IsExpanded = true;
                Settle(window);

                var sources = Grid(view, "Sync.Sources");
                var changes = Grid(view, "Sync.Changes");
                foreach (var sv in All<ScrollViewer>(changes))
                {
                    Console.WriteLine($"MEASURE {lang}: w={changes.ActualWidth:F0} viewport={sv.ViewportWidth:F0} extent={sv.ExtentWidth:F0} vbar={sv.ComputedVerticalScrollBarVisibility}");
                }

                Console.WriteLine($"COLUMNS {lang}: " + string.Join(" | ", changes.Columns.Select(column =>
                    $"{column.Header}={column.ActualWidth:F0}(min {column.MinWidth:F0})")));


                Assert(sources.ActualHeight <= 120.5,
                    $"{lang}: the expanded source table must stay bounded instead of growing to its content ({sources.ActualHeight:F0}px).");

                var scrollable = All<ScrollViewer>(sources).Any(item => item.ScrollableHeight > 0);
                Assert(scrollable,
                    "{0}: the bounded source table must scroll inside itself, otherwise its hidden rows are unreachable.".Replace("{0}", lang, StringComparison.Ordinal));

                // The reported symptom: the change table was unreachable. At
                // the smallest supported Advanced size it must keep at least
                // two rows and a header while the detail table is expanded.
                Assert(changes.IsVisible && changes.ActualHeight >= 100,
                    $"{lang}: the change table must keep a usable height while the source table is expanded ({changes.ActualHeight:F0}px).");

                var changeScroll = All<ScrollViewer>(changes).Any(item => item.ScrollableHeight > 0);
                Assert(changeScroll,
                    "{0}: the change table must scroll inside itself so its rows past the fold stay reachable.".Replace("{0}", lang, StringComparison.Ordinal));

                var bottom = changes.TranslatePoint(new Point(0, changes.ActualHeight), view).Y;
                Assert(bottom <= view.ActualHeight + 0.5,
                    $"{lang}: the change table must stay inside the workspace instead of being pushed past its bottom ({bottom:F0} > {view.ActualHeight:F0}).");

                window.Close();
                window = null;
            }

            Console.WriteLine("Recommendation updates layout (bounded source table, reachable rows, change table kept in view, AR + EN) PASS.");
        }
        finally
        {
            window?.Close();
            UiText.SetLang(original);
            UiLaySvc.Apply();
        }
    }

    private static RecSyncChange Change(int index) =>
        new($"id-{index}", RecSyncSource.ConfigApi, RecSyncChangeKind.Updated, $"BLUS0{index:D4}", $"Game {index}",
            "Core", "PPU Decoder", "Interpreter", "Recompiler (LLVM)", RecSyncDisposition.Applied, RecSyncReason.None,
            null, "https://api.rpcs3.net/config", DateTimeOffset.UnixEpoch);

    private static DataGrid Grid(DependencyObject root, string automationId) =>
        All<DataGrid>(root).Single(item => AutomationProperties.GetAutomationId(item) == automationId);

    private static void Settle(Window window)
    {
        for (var pass = 0; pass < 4; pass++)
        {
            window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
            window.UpdateLayout();
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
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(node); index++)
            {
                var child = VisualTreeHelper.GetChild(node, index);
                if (child is T match)
                {
                    found.Add(match);
                }

                Walk(child);
            }
        }
    }

    // The view binds SyncNowCmd on its own DataContext, so the stub must carry
    // it too; a missing path is a binding error the UI gate fails on.
    private sealed class Host(RecSyncVm sync)
    {
        public RecSyncVm Sync { get; } = sync;

        public System.Windows.Input.ICommand SyncNowCmd { get; } = new RelayCmd(() => { });
    }
}
