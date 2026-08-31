using System.Windows;
using System.Windows.Controls;

namespace R3Config.Ctrl;

public partial class HLchView : UserControl
{
    public static readonly RoutedEvent StartRequestedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(StartRequested),
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(HLchView));

    public static readonly RoutedEvent SavedRequestedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(SavedRequested),
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(HLchView));

    public HLchView()
    {
        InitializeComponent();
    }

    public event RoutedEventHandler StartRequested
    {
        add => AddHandler(StartRequestedEvent, value);
        remove => RemoveHandler(StartRequestedEvent, value);
    }

    public event RoutedEventHandler SavedRequested
    {
        add => AddHandler(SavedRequestedEvent, value);
        remove => RemoveHandler(SavedRequestedEvent, value);
    }

    private void RaiseStartRequested(object sender, RoutedEventArgs e)
    {
        RaiseEvent(new RoutedEventArgs(StartRequestedEvent, this));
    }

    private void RaiseSavedRequested(object sender, RoutedEventArgs e)
    {
        RaiseEvent(new RoutedEventArgs(SavedRequestedEvent, this));
    }
}
