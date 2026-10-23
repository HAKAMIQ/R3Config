using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace R3Config.Ctrl;

public static class Scrl
{
    private const double PixelWheelStep = 56.0;
    private const double LogicalWheelStep = 3.0;
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

        if (!States.TryGetValue(element, out var state))
        {
            return;
        }

        state.Reset();
        States.Remove(element);
    }

    private static void OnPreviewMouseWheel(
        object sender,
        MouseWheelEventArgs e)
    {
        if (sender is not FrameworkElement element ||
            e.Delta == 0)
        {
            return;
        }

        var state = States.GetValue(
            element,
            static owner => new ScrollState(owner.Dispatcher));

        var viewer = ResolveScrollViewer(
            element,
            e.OriginalSource as DependencyObject);

        if (viewer is null)
        {
            return;
        }

        if (!ReferenceEquals(state.Viewer, viewer))
        {
            state.Attach(viewer);
        }

        if (!state.Timer.IsEnabled)
        {
            state.TargetOffset = viewer.VerticalOffset;
        }

        var wheelSteps = e.Delta / 120.0;

        var wheelStep = viewer.CanContentScroll
            ? LogicalWheelStep
            : PixelWheelStep;

        var requestedOffset =
            state.TargetOffset -
            (wheelSteps * wheelStep);

        var maximumOffset = Math.Max(
            0.0,
            viewer.ScrollableHeight);

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
        States.Remove(element);
    }

    private static ScrollViewer? ResolveScrollViewer(
        FrameworkElement owner,
        DependencyObject? source)
    {
        var current = source;

        while (current is not null)
        {
            if (current is ScrollViewer viewer)
            {
                return viewer;
            }

            if (ReferenceEquals(current, owner))
            {
                break;
            }

            current = GetParent(current);
        }

        if (owner is Control control)
        {
            control.ApplyTemplate();
        }

        return FindScrollViewer(owner);
    }

    private static DependencyObject? GetParent(
        DependencyObject element)
    {
        if (element is Visual ||
            element is Visual3D)
        {
            return VisualTreeHelper.GetParent(element);
        }

        if (element is FrameworkContentElement contentElement)
        {
            return contentElement.Parent;
        }

        return LogicalTreeHelper.GetParent(element);
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
            var child = VisualTreeHelper.GetChild(
                parent,
                index);

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

        public ScrollViewer? Viewer { get; private set; }

        public double TargetOffset { get; set; }

        public void Attach(ScrollViewer viewer)
        {
            Timer.Stop();
            Viewer = viewer;
            TargetOffset = viewer.VerticalOffset;
        }

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
            var remainingDistance =
                TargetOffset - currentOffset;

            if (Math.Abs(remainingDistance) <= StopThreshold)
            {
                Viewer.ScrollToVerticalOffset(TargetOffset);
                Timer.Stop();
                return;
            }

            var nextOffset =
                currentOffset +
                (remainingDistance * Ease);

            Viewer.ScrollToVerticalOffset(
                Math.Clamp(
                    nextOffset,
                    0.0,
                    maximumOffset));
        }
    }
}