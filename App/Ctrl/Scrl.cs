using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace R3Config.Ctrl;

public static class Scrl
{
    private const double WheelStep = 56.0;
    private const double Ease = 0.22;
    private const double StopThreshold = 0.5;

    private static readonly ConditionalWeakTable<FrameworkElement, ScrollState>
        States = new();

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(Scrl),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static void SetIsEnabled(
        DependencyObject element,
        bool value)
    {
        element.SetValue(IsEnabledProperty, value);
    }

    public static bool GetIsEnabled(DependencyObject element)
    {
        return (bool)element.GetValue(IsEnabledProperty);
    }

    private static void OnIsEnabledChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not FrameworkElement element)
        {
            return;
        }

        element.PreviewMouseWheel -= OnPreviewMouseWheel;
        element.Unloaded -= OnUnloaded;

        if (e.NewValue is true)
        {
            element.PreviewMouseWheel += OnPreviewMouseWheel;
            element.Unloaded += OnUnloaded;
            return;
        }

        if (States.TryGetValue(element, out var state))
        {
            state.Reset();
            States.Remove(element);
        }
    }

    private static void OnPreviewMouseWheel(
        object sender,
        MouseWheelEventArgs e)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        var state = States.GetValue(
            element,
            static owner => new ScrollState(owner.Dispatcher));

        state.Viewer ??= FindScrollViewer(element);

        if (state.Viewer is null)
        {
            if (element is Control control)
            {
                control.ApplyTemplate();
            }

            state.Viewer = FindScrollViewer(element);
        }

        if (state.Viewer is null)
        {
            return;
        }

        if (!state.Timer.IsEnabled)
        {
            state.TargetOffset = state.Viewer.VerticalOffset;
        }

        var wheelSteps = e.Delta / 120.0;
        var requestedOffset =
            state.TargetOffset -
            (wheelSteps * WheelStep);

        var maximumOffset = Math.Max(
            0.0,
            state.Viewer.ScrollableHeight);

        var nextOffset = Math.Clamp(
            requestedOffset,
            0.0,
            maximumOffset);

        if (Math.Abs(nextOffset - state.TargetOffset) < StopThreshold)
        {
            return;
        }

        state.TargetOffset = nextOffset;

        if (!state.Timer.IsEnabled)
        {
            state.Timer.Start();
        }

        e.Handled = true;
    }

    private static void OnUnloaded(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element ||
            !States.TryGetValue(element, out var state))
        {
            return;
        }

        state.Reset();
    }

    private static ScrollViewer? FindScrollViewer(
        DependencyObject parent)
    {
        if (parent is ScrollViewer viewer)
        {
            return viewer;
        }

        var childCount = VisualTreeHelper.GetChildrenCount(parent);

        for (var index = 0; index < childCount; index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            var nestedViewer = FindScrollViewer(child);

            if (nestedViewer is not null)
            {
                return nestedViewer;
            }
        }

        return null;
    }

    private sealed class ScrollState
    {
        public ScrollState(Dispatcher dispatcher)
        {
            Timer = new DispatcherTimer(
                TimeSpan.FromMilliseconds(16),
                DispatcherPriority.Render,
                OnTick,
                dispatcher);

            Timer.Stop();
        }

        public DispatcherTimer Timer { get; }

        public ScrollViewer? Viewer { get; set; }

        public double TargetOffset { get; set; }

        public void Reset()
        {
            Timer.Stop();
            Viewer = null;
            TargetOffset = 0.0;
        }

        private void OnTick(
            object? sender,
            EventArgs e)
        {
            if (Viewer is null)
            {
                Timer.Stop();
                return;
            }

            var maximumOffset = Math.Max(
                0.0,
                Viewer.ScrollableHeight);

            TargetOffset = Math.Clamp(
                TargetOffset,
                0.0,
                maximumOffset);

            var currentOffset = Viewer.VerticalOffset;
            var remainingDistance = TargetOffset - currentOffset;

            if (Math.Abs(remainingDistance) <= StopThreshold)
            {
                Viewer.ScrollToVerticalOffset(TargetOffset);
                Timer.Stop();
                return;
            }

            Viewer.ScrollToVerticalOffset(
                currentOffset +
                (remainingDistance * Ease));
        }
    }
}