using R3Config.Mdl;

namespace R3Config.Vm;

public sealed class GameVm : VmBase
{
    private RecSum _recStat;

    public GameVm(
        Rpcs3GameDescriptor game,
        WikiRec? recommendation,
        RecSum recState,
        string serial,
        string title,
        bool isDetectedInRpcs3)
    {
        Game = game;
        Rec = recommendation;
        _recStat = recState;
        Serial = serial;
        Title = title;
        InR3 = isDetectedInRpcs3;
    }

    public Rpcs3GameDescriptor Game { get; }

    public WikiRec? Rec { get; }

    public RecSum RecState
    {
        get => _recStat;
        set
        {
            if (Set(ref _recStat, value))
            {
                OnChange(nameof(Pending));
            }
        }
    }

    public string Serial { get; }

    public string Title { get; }

    public bool InR3 { get; }

    public int Pending => RecState.Pending;
}
