using System.Windows;
using System.Windows.Controls;

namespace R3Config.Ctrl;

public partial class HSavView : UserControl
{
    public static readonly RoutedEvent CloseRequestedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(CloseRequested),
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(HSavView));

    public static readonly RoutedEvent OpenRequestedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(OpenRequested),
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(HSavView));

    public HSavView()
    {
        InitializeComponent();
    }

    public event RoutedEventHandler CloseRequested
    {
        add => AddHandler(CloseRequestedEvent, value);
        remove => RemoveHandler(CloseRequestedEvent, value);
    }

    public event RoutedEventHandler OpenRequested
    {
        add => AddHandler(OpenRequestedEvent, value);
        remove => RemoveHandler(OpenRequestedEvent, value);
    }

    private void RaiseCloseRequested(object sender, RoutedEventArgs e)
    {
        RaiseEvent(new RoutedEventArgs(CloseRequestedEvent, this));
    }

    private void RaiseOpenRequested(object sender, RoutedEventArgs e)
    {
        RaiseEvent(new RoutedEventArgs(OpenRequestedEvent, this));
    }
}
