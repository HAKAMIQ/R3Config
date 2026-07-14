using R3Config.Mdl;

namespace R3Config.Vm;

public sealed class GameItemVm : VmBase
{
    private RecSum _recState;

    public GameItemVm(
        R3Game game,
        WikiRec? recommendation,
        RecSum recState,
        string serial,
        string title,
        bool isDetectedInRpcs3)
    {
        Game = game;
        Recommendation = recommendation;
        _recState = recState;
        Serial = serial;
        Title = title;
        IsDetectedInRpcs3 = isDetectedInRpcs3;
    }

    public R3Game Game { get; }

    public WikiRec? Recommendation { get; }

    public RecSum RecState
    {
        get => _recState;
        set
        {
            if (SetProperty(ref _recState, value))
            {
                OnPropertyChanged(nameof(Pending));
            }
        }
    }

    public string Serial { get; }

    public string Title { get; }

    public bool IsDetectedInRpcs3 { get; }

    public int Pending => RecState.Pending;
}
