using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using R3Config.Lang;

namespace R3Config.Vm;

public sealed class ListVm : VmBase
{
    private readonly List<GameVm> _allGame = [];
    private readonly BulkList<GameVm> _games = [];
    private GameVm? _selGame;
    private bool _inR3 = true;
    private string _text = string.Empty;

    public ListVm()
    {
        Games.CollectionChanged += (_, _) =>
        {
            OnChange(nameof(IsEmpty));
            OnChange(nameof(HasGames));
        };
    }

    public ObservableCollection<GameVm> Games => _games;

    public bool IsEmpty => Games.Count == 0;

    public bool HasGames => Games.Count > 0;

    public bool IsSearch => string.IsNullOrWhiteSpace(Text);

    public bool InR3
    {
        get => _inR3;
        set
        {
            if (Set(ref _inR3, value))
            {
                ApplySel();
                OnChange(nameof(EmptyTxt));
                OnChange(nameof(Tip));
            }
        }
    }

    public string Text
    {
        get => _text;
        set
        {
            if (Set(ref _text, value))
            {
                ApplySel();
                OnChange(nameof(IsSearch));
                OnChange(nameof(EmptyTxt));
            }
        }
    }

    public string EmptyTxt => !IsSearch
        ? UiText.GetText("Games.EmptySearch")
        : InR3
            ? UiText.GetText("Games.EmptyInstalled")
            : UiText.GetText("Games.Empty");

    public string Tip => UiText.GetText("Games.FilterDetectedOnlyTip");

    public GameVm? SelGame
    {
        get => _selGame;
        set => Set(ref _selGame, value);
    }

    public void Load(IEnumerable<GameVm> games)
    {
        _allGame.Clear();
        _allGame.AddRange(games.OrderBy(game => game.Title).ThenBy(game => game.Serial));

        ApplySel();
    }

    public void Clear()
    {
        _allGame.Clear();
        _games.Replace([]);
        SelGame = null;
        Text = string.Empty;

        OnChange(nameof(IsEmpty));
        OnChange(nameof(HasGames));
        OnChange(nameof(EmptyTxt));
    }

    public void SetLang()
    {
        OnChange(nameof(EmptyTxt));
        OnChange(nameof(Tip));
        OnChange(nameof(IsSearch));
    }

    private void ApplySel()
    {
        var selectedSerial = SelGame?.Serial;
        var selectedTitle = SelGame?.Title;

        var search = Text.Trim();

        var visible = InR3
            ? _allGame.Where(game => game.InR3).ToArray()
            : _allGame.ToArray();

        if (!string.IsNullOrWhiteSpace(search))
        {
            visible = visible
                .Where(game =>
                    game.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    game.Serial.Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        _games.Replace(visible);

        var selected = string.IsNullOrWhiteSpace(selectedSerial)
            ? null
            : Games.FirstOrDefault(game =>
                string.Equals(game.Serial, selectedSerial, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(game.Title, selectedTitle, StringComparison.OrdinalIgnoreCase));

        SelGame = selected;

        OnChange(nameof(IsEmpty));
        OnChange(nameof(HasGames));
        OnChange(nameof(EmptyTxt));
    }

    private sealed class BulkList<T> : ObservableCollection<T>
    {
        public void Replace(IEnumerable<T> items)
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
