using System.Windows.Controls;
using System.Windows.Automation.Peers;

namespace R3Config.Ctrl;

public partial class POffView : UserControl
{
    public POffView()
    {
        InitializeComponent();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new AccessibleUserControlPeer(this);
}
