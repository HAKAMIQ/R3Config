using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace R3Config.Ctrl;

public partial class HInsView : UserControl
{
    public HInsView()
    {
        InitializeComponent();
    }

    public void Reset()
    {
        AnchorsPanel.IsExpanded = false;
    }

    private void OpenHelp_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.ContextMenu is not { } menu)
        {
            return;
        }

        menu.PlacementTarget = button;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
        e.Handled = true;
    }
}
