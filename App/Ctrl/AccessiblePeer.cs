using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;

namespace R3Config.Ctrl;

internal sealed class AccessibleUserControlPeer : FrameworkElementAutomationPeer
{
    private readonly FrameworkElement _owner;
    private readonly TabControl? _tabs;

    // tabs: an inner TabControl styled with FluentTabControl, whose template
    // has no PART_SelectedContentHost, so WPF's TabItem peer exposes none of
    // the selected tab's content. Its selected content is added after the
    // tab headers, as MainAutomationPeer does for the main tabs.
    public AccessibleUserControlPeer(FrameworkElement owner, TabControl? tabs = null)
        : base(owner)
    {
        _owner = owner;
        _tabs = tabs;
    }

    protected override string GetClassNameCore() => _owner.GetType().Name;
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;
    protected override string GetNameCore() => AutomationProperties.GetName(_owner) ?? string.Empty;

    protected override List<AutomationPeer>? GetChildrenCore()
    {
        var peers = new List<AutomationPeer>();
        Collect(_owner, peers, 0);
        if (_tabs?.SelectedContent is UIElement content)
        {
            var peer = UIElementAutomationPeer.CreatePeerForElement(content);
            if (peer is null) Collect(content, peers, 0);
            else if (!peers.Contains(peer)) peers.Add(peer);
        }

        return peers.Count == 0 ? null : peers;
    }

    private static void Collect(DependencyObject node, List<AutomationPeer> peers, int depth)
    {
        if (depth > 12) return;
        var count = VisualTreeHelper.GetChildrenCount(node);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(node, i);
            if (child is UIElement element)
            {
                var peer = UIElementAutomationPeer.CreatePeerForElement(element);
                if (peer is not null) peers.Add(peer);
                else Collect(child, peers, depth + 1);
            }
        }
    }
}
