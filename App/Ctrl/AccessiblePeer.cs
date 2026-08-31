using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Media;

namespace R3Config.Ctrl;

/// <summary>Exposes realized control descendants of composed UserControls to UIA.</summary>
internal sealed class AccessibleUserControlPeer : FrameworkElementAutomationPeer
{
    private readonly FrameworkElement _owner;

    public AccessibleUserControlPeer(FrameworkElement owner)
        : base(owner)
    {
        _owner = owner;
    }

    protected override string GetClassNameCore() => _owner.GetType().Name;
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;
    protected override string GetNameCore() => AutomationProperties.GetName(_owner) ?? string.Empty;

    protected override List<AutomationPeer>? GetChildrenCore()
    {
        var peers = new List<AutomationPeer>();
        Collect(_owner, peers, 0);
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
