using System.Windows;
using System.Windows.Controls;

namespace R3Config.Ctrl;

public enum PatchViewMode
{
    Official,
    Canary,
    Cheat
}

public partial class PatchView : UserControl
{
    public static readonly DependencyProperty ModeProperty = DependencyProperty.Register(
        nameof(Mode),
        typeof(PatchViewMode),
        typeof(PatchView),
        new PropertyMetadata(PatchViewMode.Official, OnModeChanged));

    public static readonly DependencyProperty SelectedIndexProperty = DependencyProperty.Register(
        nameof(SelectedIndex),
        typeof(int),
        typeof(PatchView),
        new PropertyMetadata(0));

    public PatchView()
    {
        InitializeComponent();
        UpdateSelectedIndex();
    }

    public PatchViewMode Mode
    {
        get => (PatchViewMode)GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty);
        private set => SetValue(SelectedIndexProperty, value);
    }

    private static void OnModeChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        if (dependencyObject is PatchView view)
        {
            view.UpdateSelectedIndex();
        }
    }

    private void UpdateSelectedIndex()
    {
        SelectedIndex = Mode switch
        {
            PatchViewMode.Canary => 1,
            PatchViewMode.Cheat => 2,
            _ => 0
        };
    }
}