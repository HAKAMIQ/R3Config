using System.Windows;
using System.Windows.Controls;

namespace R3Config.Svc;

internal static class ToolTipPolicy
{
    internal const int InitialShowDelayMs = 600;
    internal const int BetweenShowDelayMs = 150;
    internal const int ShowDurationMs = 6000;

    private static bool _registered;

    internal static void Register()
    {
        if (_registered)
        {
            return;
        }

        EventManager.RegisterClassHandler(
            typeof(FrameworkElement),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(ApplyOnLoaded),
            handledEventsToo: true);

        _registered = true;
    }

    private static void ApplyOnLoaded(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element ||
            ToolTipService.GetToolTip(element) is null)
        {
            return;
        }

        ToolTipService.SetInitialShowDelay(
            element,
            InitialShowDelayMs);
        ToolTipService.SetBetweenShowDelay(
            element,
            BetweenShowDelayMs);
        ToolTipService.SetShowDuration(
            element,
            ShowDurationMs);
        ToolTipService.SetShowOnDisabled(
            element,
            true);
    }
}
