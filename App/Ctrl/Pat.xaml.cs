using System.Windows;
using System.Windows.Controls;

namespace R3Config.Ctrl;

public enum PatchViewMode
{
    Patches,
    Canary,
    Cheat
}

public partial class PatchView : UserControl
{
    public static readonly DependencyProperty ModeProperty = DependencyProperty.Register(
        nameof(Mode),
        typeof(PatchViewMode),
        typeof(PatchView),
        new PropertyMetadata(PatchViewMode.Patches, OnModeChanged));

    public PatchView()
    {
        InitializeComponent();
        UpdateMode();
    }

    public PatchViewMode Mode
    {
        get => (PatchViewMode)GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    private static void OnModeChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs eventArgs)
    {
        if (dependencyObject is PatchView view)
        {
            view.UpdateMode();
        }
    }

    private void UpdateMode()
    {
        if (PatchesRoot is null ||
            CheatRoot is null ||
            OfficialSourcePanel is null ||
            CanarySourcePanel is null)
        {
            return;
        }

        var showPatchLibrary = Mode != PatchViewMode.Cheat;
        PatchesRoot.Visibility = showPatchLibrary
            ? Visibility.Visible
            : Visibility.Collapsed;
        CheatRoot.Visibility = showPatchLibrary
            ? Visibility.Collapsed
            : Visibility.Visible;
        OfficialSourcePanel.Visibility = Mode == PatchViewMode.Patches
            ? Visibility.Visible
            : Visibility.Collapsed;
        CanarySourcePanel.Visibility = Mode == PatchViewMode.Canary
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (showPatchLibrary)
        {
            ShowCanary(Mode == PatchViewMode.Canary);
        }
    }

    private void ShowCanary(bool showCanary)
    {
        if (OfficialContent is null || CanaryContent is null)
        {
            return;
        }

        OfficialContent.Visibility = showCanary
            ? Visibility.Collapsed
            : Visibility.Visible;
        CanaryContent.Visibility = showCanary
            ? Visibility.Visible
            : Visibility.Collapsed;

    }
}
