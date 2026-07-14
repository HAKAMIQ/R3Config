using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using R3Config.Lang;

namespace R3Config.Vm;

public sealed class GameListVm : VmBase
{
    private readonly List<GameItemVm> _allGames = [];
    private readonly BulkObservableCollection<GameItemVm> _games = [];
    private GameItemVm? _selectedGame;
    private bool _showOnlyDetectedInRpcs3 = true;
    private string _searchText = string.Empty;

    public GameListVm()
    {
        Games.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(HasGames));
        };
    }

    public ObservableCollection<GameItemVm> Games => _games;

    public bool IsEmpty => Games.Count == 0;

    public bool HasGames => Games.Count > 0;

    public bool IsSearchEmpty => string.IsNullOrWhiteSpace(SearchText);

    public bool ShowOnlyDetectedInRpcs3
    {
        get => _showOnlyDetectedInRpcs3;
        set
        {
            if (SetProperty(ref _showOnlyDetectedInRpcs3, value))
            {
                ApplyFilterPreservingSelection();
                OnPropertyChanged(nameof(EmptyStateText));
                OnPropertyChanged(nameof(FilterTooltip));
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilterPreservingSelection();
                OnPropertyChanged(nameof(IsSearchEmpty));
                OnPropertyChanged(nameof(EmptyStateText));
            }
        }
    }

    public string EmptyStateText => !IsSearchEmpty
        ? UiText.T("Games.EmptySearch")
        : ShowOnlyDetectedInRpcs3
            ? UiText.T("Games.EmptyInstalled")
            : UiText.T("Games.Empty");

    public string FilterTooltip => UiText.T("Games.FilterDetectedOnlyTip");

    public GameItemVm? SelectedGame
    {
        get => _selectedGame;
        set => SetProperty(ref _selectedGame, value);
    }

    public void Load(IEnumerable<GameItemVm> games)
    {
        _allGames.Clear();
        _allGames.AddRange(games.OrderBy(game => game.Title).ThenBy(game => game.Serial));

        ApplyFilterPreservingSelection();
    }

    public void Clear()
    {
        _allGames.Clear();
        _games.ReplaceAll([]);
        SelectedGame = null;
        SearchText = string.Empty;

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasGames));
        OnPropertyChanged(nameof(EmptyStateText));
    }

    public void RefreshLanguage()
    {
        OnPropertyChanged(nameof(EmptyStateText));
        OnPropertyChanged(nameof(FilterTooltip));
        OnPropertyChanged(nameof(IsSearchEmpty));
    }

    private void ApplyFilterPreservingSelection()
    {
        var selectedSerial = SelectedGame?.Serial;
        var selectedTitle = SelectedGame?.Title;

        var search = SearchText.Trim();

        var visible = ShowOnlyDetectedInRpcs3
            ? _allGames.Where(game => game.IsDetectedInRpcs3).ToArray()
            : _allGames.ToArray();

        if (!string.IsNullOrWhiteSpace(search))
        {
            visible = visible
                .Where(game =>
                    game.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    game.Serial.Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        _games.ReplaceAll(visible);

        var selected = string.IsNullOrWhiteSpace(selectedSerial)
            ? null
            : Games.FirstOrDefault(game =>
                string.Equals(game.Serial, selectedSerial, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(game.Title, selectedTitle, StringComparison.OrdinalIgnoreCase));

        SelectedGame = selected;

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasGames));
        OnPropertyChanged(nameof(EmptyStateText));
    }

    private sealed class BulkObservableCollection<T> : ObservableCollection<T>
    {
        public void ReplaceAll(IEnumerable<T> items)
        {
            Items.Clear();

            foreach (var item in items)
            {
                Items.Add(item);
            }

            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Reset));
        }
    }
}
