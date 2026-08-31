using System.Windows.Controls;
using System.Windows.Automation.Peers;

namespace R3Config.Ctrl;

public partial class SetupView : UserControl
{
    public SetupView()
    {
        InitializeComponent();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new AccessibleUserControlPeer(this);
}
