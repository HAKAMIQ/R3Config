using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace R3Config.Ctrl;

public partial class RuntimeView : UserControl
{
    public RuntimeView()
    {
        InitializeComponent();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new AccessibleUserControlPeer(this);
}
