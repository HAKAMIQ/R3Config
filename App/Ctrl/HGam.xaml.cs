using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace R3Config.Ctrl;

public partial class HGamView : UserControl
{
    public HGamView()
    {
        InitializeComponent();
    }

    private void GameSearchChanged(
        object sender,
        TextChangedEventArgs e)
    {
        GameSelectionList.Items.Refresh();
    }

    private void GameSelectionFilter(
        object sender,
        FilterEventArgs e)
    {
        if (e.Item is null)
        {
            e.Accepted = false;
            return;
        }

        var detectedProperty =
            e.Item.GetType().GetProperty("InR3");

        var isDetected =
            detectedProperty?.GetValue(e.Item)
            is bool detected &&
            detected;

        if (!isDetected)
        {
            e.Accepted = false;
            return;
        }

        var query = GameSearchBox.Text?.Trim();

        if (string.IsNullOrWhiteSpace(query))
        {
            e.Accepted = true;
            return;
        }

        var game =
            e.Item.GetType()
                .GetProperty("Game")
                ?.GetValue(e.Item);

        if (game is null)
        {
            e.Accepted = false;
            return;
        }

        e.Accepted =
            ContainsGameValue(game, "Title", query) ||
            ContainsGameValue(game, "Serial", query) ||
            ContainsGameValue(game, "Version", query);
    }

    private static bool ContainsGameValue(
        object game,
        string propertyName,
        string query)
    {
        var value =
            game.GetType()
                .GetProperty(propertyName)
                ?.GetValue(game)
                ?.ToString();

        return
            !string.IsNullOrWhiteSpace(value) &&
            value.Contains(
                query,
                StringComparison.OrdinalIgnoreCase);
    }
}
