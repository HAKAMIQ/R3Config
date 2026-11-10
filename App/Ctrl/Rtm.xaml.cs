using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Threading;

namespace R3Config.Ctrl;

public partial class RuntimeView : UserControl
{
    public RuntimeView()
    {
        InitializeComponent();

        // TabControl replaces SelectedContent after SelectionChanged is raised;
        // refresh this view's automation children once the new content is in,
        // so screen readers see the selected inner tab, not the previous one.
        Tabs.SelectionChanged += (_, e) =>
        {
            if (ReferenceEquals(e.OriginalSource, Tabs))
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded,
                    () => UIElementAutomationPeer.FromElement(this)?.ResetChildrenCache());
            }
        };
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new AccessibleUserControlPeer(this, Tabs);
}
