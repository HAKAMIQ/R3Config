using System.Windows;
using System.Windows.Controls;

namespace R3Config.Ctrl;

public partial class PChtView : UserControl
{
    public PChtView()
    {
        InitializeComponent();
    }

    private void OpenArtemisOptions_Click(
        object sender,
        RoutedEventArgs eventArgs)
    {
        if (sender is not Button button || button.ContextMenu is null)
        {
            return;
        }

        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.IsOpen = true;
        eventArgs.Handled = true;
    }
}
