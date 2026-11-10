using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using R3Config.Svc;

namespace R3Config.Ctrl;

public partial class GameList : UserControl
{
    public GameList()
    {
        InitializeComponent();
    }

    private void CheckBox_Checked(object sender, RoutedEventArgs e)
    {

    }

    private void ToolTipOwner_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            ToolTipPolicy.Apply(element);
        }
    }

    private void GameImagePreview_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.Tag is not Image sourceImage ||
            sourceImage.Source is null)
        {
            return;
        }

        if (GameImagePreviewPopup.IsOpen)
        {
            GameImagePreviewPopup.IsOpen = false;
        }

        GameImagePreviewPopup.PlacementTarget = button;
        GameImagePreviewImage.Source = sourceImage.Source;
        SizePreviewToPixels(sourceImage.Source as BitmapSource);
        // A list refresh can recycle the anchor row; the preview never
        // outlives the element it points at.
        button.Unloaded += GameImagePreviewAnchor_Unloaded;
        GameImagePreviewPopup.IsOpen = true;
        e.Handled = true;
    }

    // Full size: one bitmap pixel per screen pixel at the current DPI, shrunk
    // uniformly only when the work area cannot hold it with the popup chrome.
    private void SizePreviewToPixels(BitmapSource? bitmap)
    {
        if (bitmap is null)
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        GameImagePreviewImage.Width = bitmap.PixelWidth / dpi.DpiScaleX;
        GameImagePreviewImage.Height = bitmap.PixelHeight / dpi.DpiScaleY;
        GameImagePreviewImage.MaxWidth = Math.Max(1, SystemParameters.WorkArea.Width - PreviewChrome);
        GameImagePreviewImage.MaxHeight = Math.Max(1, SystemParameters.WorkArea.Height - PreviewChrome);
    }

    private const double PreviewChrome = 96;

    private void GameImagePreviewAnchor_Unloaded(object sender, RoutedEventArgs e)
    {
        GameImagePreviewPopup.IsOpen = false;
    }

    // Escape dismisses the preview like any Fluent popover and returns focus
    // to the button that opened it.
    private void GameImagePreviewPopup_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        CloseGameImagePreview();
        e.Handled = true;
    }

    private void CloseGameImagePreview()
    {
        var owner = GameImagePreviewPopup.PlacementTarget as UIElement;
        GameImagePreviewPopup.IsOpen = false;
        owner?.Focus();
    }

    private void GameImagePreviewPopup_Opened(object? sender, EventArgs e)
    {
        var duration = (Duration)FindResource("Motion.Surface");
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

        GameImagePreviewSurface.Opacity = 0;
        GameImagePreviewScale.ScaleX = 0.96;
        GameImagePreviewScale.ScaleY = 0.96;

        GameImagePreviewSurface.BeginAnimation(
            UIElement.OpacityProperty,
            new DoubleAnimation(0, 1, duration)
            {
                EasingFunction = easing
            });

        GameImagePreviewScale.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            new DoubleAnimation(0.96, 1, duration)
            {
                EasingFunction = easing
            });

        GameImagePreviewScale.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            new DoubleAnimation(0.96, 1, duration)
            {
                EasingFunction = easing
            });

        // Keyboard users land inside the preview so Escape and Tab work.
        GameImagePreviewCloseButton.Focus();
    }

    private void GameImagePreviewPopup_Closed(object? sender, EventArgs e)
    {
        GameImagePreviewSurface.BeginAnimation(UIElement.OpacityProperty, null);
        GameImagePreviewScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        GameImagePreviewScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        GameImagePreviewSurface.Opacity = 0;
        GameImagePreviewScale.ScaleX = 0.96;
        GameImagePreviewScale.ScaleY = 0.96;
        GameImagePreviewImage.Source = null;
        if (GameImagePreviewPopup.PlacementTarget is FrameworkElement anchor)
        {
            anchor.Unloaded -= GameImagePreviewAnchor_Unloaded;
        }

        GameImagePreviewPopup.PlacementTarget = null;
    }

    private void GameImagePreviewClose_Click(object sender, RoutedEventArgs e)
    {
        CloseGameImagePreview();
        e.Handled = true;
    }
}
