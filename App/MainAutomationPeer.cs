using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace R3Config;

internal sealed class MainAutomationPeer(MainWin owner) : WindowAutomationPeer(owner)
{
    protected override List<AutomationPeer>? GetChildrenCore()
    {
        var peers = base.GetChildrenCore() ?? [];
        if (owner.Tabs.SelectedContent is UIElement content)
        {
            var peer = UIElementAutomationPeer.CreatePeerForElement(content);
            if (peer is not null && !peers.Contains(peer)) peers.Add(peer);
        }
        return peers;
    }
}
