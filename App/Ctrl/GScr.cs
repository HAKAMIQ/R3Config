using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace R3Config.Ctrl;

public partial class GameList
{
    private const double GameListWheelStep = 56.0;
    private const double GameListScrollEase = 0.22;
    private const double GameListScrollStopThreshold = 0.5;

    private ScrollViewer? _gameListScrollViewer;
    private DispatcherTimer? _gameListScrollTimer;
    private double _gameListTargetOffset;

    private void GamesList_PreviewMouseWheel(
        object sender,
        MouseWheelEventArgs e)
    {
        if (sender is not ListBox listBox)
        {
            return;
        }

        _gameListScrollViewer ??=
            FindVisualChild<ScrollViewer>(listBox);

        if (_gameListScrollViewer is null)
        {
            listBox.ApplyTemplate();

            _gameListScrollViewer =
                FindVisualChild<ScrollViewer>(listBox);
        }

        if (_gameListScrollViewer is null)
        {
            return;
        }

        EnsureGameListScrollTimer();

        if (_gameListScrollTimer is null)
        {
            return;
        }

        if (!_gameListScrollTimer.IsEnabled)
        {
            _gameListTargetOffset =
                _gameListScrollViewer.VerticalOffset;
        }

        double wheelSteps = e.Delta / 120.0;

        double requestedOffset =
            _gameListTargetOffset -
            (wheelSteps * GameListWheelStep);

        double maximumOffset =
            Math.Max(
                0.0,
                _gameListScrollViewer.ScrollableHeight);

        double nextOffset =
            Math.Clamp(
                requestedOffset,
                0.0,
                maximumOffset);

        if (Math.Abs(
                nextOffset -
                _gameListTargetOffset) <
            GameListScrollStopThreshold)
        {
            return;
        }

        _gameListTargetOffset = nextOffset;

        if (!_gameListScrollTimer.IsEnabled)
        {
            _gameListScrollTimer.Start();
        }

        e.Handled = true;
    }

    private void GamesList_Unloaded(
        object sender,
        RoutedEventArgs e)
    {
        if (_gameListScrollTimer is not null)
        {
            _gameListScrollTimer.Stop();
        }

        _gameListScrollViewer = null;
        _gameListTargetOffset = 0.0;
    }

    private void EnsureGameListScrollTimer()
    {
        if (_gameListScrollTimer is not null)
        {
            return;
        }

        _gameListScrollTimer =
            new DispatcherTimer(
                DispatcherPriority.Render)
            {
                Interval =
                    TimeSpan.FromMilliseconds(16)
            };

        _gameListScrollTimer.Tick +=
            GameListScrollTimer_Tick;
    }

    private void GameListScrollTimer_Tick(
        object? sender,
        EventArgs e)
    {
        if (
            _gameListScrollViewer is null ||
            _gameListScrollTimer is null)
        {
            return;
        }

        double maximumOffset =
            Math.Max(
                0.0,
                _gameListScrollViewer.ScrollableHeight);

        _gameListTargetOffset =
            Math.Clamp(
                _gameListTargetOffset,
                0.0,
                maximumOffset);

        double currentOffset =
            _gameListScrollViewer.VerticalOffset;

        double remainingDistance =
            _gameListTargetOffset -
            currentOffset;

        if (
            Math.Abs(remainingDistance) <=
            GameListScrollStopThreshold)
        {
            _gameListScrollViewer
                .ScrollToVerticalOffset(
                    _gameListTargetOffset);

            _gameListScrollTimer.Stop();

            return;
        }

        double nextOffset =
            currentOffset +
            (remainingDistance *
             GameListScrollEase);

        _gameListScrollViewer
            .ScrollToVerticalOffset(
                nextOffset);
    }

    private static T? FindVisualChild<T>(
        DependencyObject parent)
        where T : DependencyObject
    {
        int childCount =
            VisualTreeHelper.GetChildrenCount(
                parent);

        for (
            int index = 0;
            index < childCount;
            index++)
        {
            DependencyObject child =
                VisualTreeHelper.GetChild(
                    parent,
                    index);

            if (child is T result)
            {
                return result;
            }

            T? nestedResult =
                FindVisualChild<T>(child);

            if (nestedResult is not null)
            {
                return nestedResult;
            }
        }

        return null;
    }
}