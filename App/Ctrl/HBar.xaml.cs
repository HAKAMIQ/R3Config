using System.Windows;
using System.Windows.Controls;

namespace R3Config.Ctrl;

public partial class HBarView : UserControl
{
    public static readonly DependencyProperty SelectedIndexProperty =
        DependencyProperty.Register(
            nameof(SelectedIndex),
            typeof(int),
            typeof(HBarView),
            new PropertyMetadata(0));

    public static readonly RoutedEvent PreviousRequestedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(PreviousRequested),
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(HBarView));

    public static readonly RoutedEvent NextRequestedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(NextRequested),
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(HBarView));

    public HBarView()
    {
        InitializeComponent();
    }

    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    public bool NextEnabled
    {
        get => NextButton.IsEnabled;
        set => NextButton.IsEnabled = value;
    }

    public event RoutedEventHandler PreviousRequested
    {
        add => AddHandler(PreviousRequestedEvent, value);
        remove => RemoveHandler(PreviousRequestedEvent, value);
    }

    public event RoutedEventHandler NextRequested
    {
        add => AddHandler(NextRequestedEvent, value);
        remove => RemoveHandler(NextRequestedEvent, value);
    }

    private void RaisePreviousRequested(object sender, RoutedEventArgs e)
    {
        RaiseEvent(new RoutedEventArgs(PreviousRequestedEvent, this));
    }

    private void RaiseNextRequested(object sender, RoutedEventArgs e)
    {
        RaiseEvent(new RoutedEventArgs(NextRequestedEvent, this));
    }

    private void ReturnToGames(object sender, RoutedEventArgs e)
    {
        Window.GetWindow(this)?.Close();
    }

    private void OpenMore(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.ContextMenu is null)
        {
            return;
        }

        button.ContextMenu.DataContext = button.DataContext;
        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.IsOpen = true;
    }
}
