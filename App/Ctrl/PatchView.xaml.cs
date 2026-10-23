using System.Windows;
using System.Windows.Controls;

namespace R3Config.Ctrl;

public enum PatMode
{
    Patches,
    Canary,
    Cheat
}

public partial class PatchView : UserControl
{
    public static readonly DependencyProperty ModeProp = DependencyProperty.Register(
        nameof(Mode),
        typeof(PatMode),
        typeof(PatchView),
        new PropertyMetadata(PatMode.Patches, ModeChg));

    public PatchView()
    {
        InitializeComponent();
        SetMode();
    }

    public PatMode Mode
    {
        get => (PatMode)GetValue(ModeProp);
        set => SetValue(ModeProp, value);
    }

    private static void ModeChg(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs eventArgs)
    {
        if (dependencyObject is PatchView view)
        {
            view.SetMode();
        }
    }

    private void SetMode()
    {
        if (PatRoot is null ||
            ChtRoot is null ||
            SourceArea is null)
        {
            return;
        }

        var showPatchLibrary = Mode != PatMode.Cheat;
        PatRoot.Visibility = Visibility.Visible;
        ChtRoot.Visibility = showPatchLibrary
            ? Visibility.Collapsed
            : Visibility.Visible;
        SourceArea.Visibility = Mode == PatMode.Patches
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (showPatchLibrary)
        {
            ShowCan(Mode == PatMode.Canary);
        }
        else
        {
            OfclBody.Visibility = Visibility.Collapsed;
            CanBody.Visibility = Visibility.Collapsed;
        }
    }

    private void ShowCan(bool showCanary)
    {
        if (OfclBody is null || CanBody is null)
        {
            return;
        }

        OfclBody.Visibility = showCanary
            ? Visibility.Collapsed
            : Visibility.Visible;
        CanBody.Visibility = showCanary
            ? Visibility.Visible
            : Visibility.Collapsed;
    }
}
