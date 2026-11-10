using System;
using System.Windows.Input;

namespace R3Config.Vm;

// The quiet main-window notice that the selected game's latest RPCS3 log
// session recorded a crash the user has not opened or dismissed yet. It only
// holds the state; MainVm decides when it shows and stores the acknowledgement.
public sealed class LogCrashNoticeVm : VmBase
{
    private bool _isVisible;

    public LogCrashNoticeVm(Action open, Action dismiss)
    {
        ArgumentNullException.ThrowIfNull(open);
        ArgumentNullException.ThrowIfNull(dismiss);
        OpenCmd = new RelayCmd(open, () => IsVisible);
        DismissCmd = new RelayCmd(dismiss, () => IsVisible);
    }

    public bool IsVisible
    {
        get => _isVisible;
        private set
        {
            if (Set(ref _isVisible, value))
            {
                ((RelayCmd)OpenCmd).RaiseCan();
                ((RelayCmd)DismissCmd).RaiseCan();
            }
        }
    }

    // The game serial and LogCrashNotice.SessionId the notice is about.
    public string? Serial { get; private set; }

    public string? Session { get; private set; }

    public ICommand OpenCmd { get; }

    public ICommand DismissCmd { get; }

    internal void Show(string serial, string session)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serial);
        ArgumentException.ThrowIfNullOrWhiteSpace(session);
        Serial = serial;
        Session = session;
        OnChange(nameof(Serial));
        OnChange(nameof(Session));
        IsVisible = true;
    }

    internal void Hide()
    {
        IsVisible = false;
        Serial = null;
        Session = null;
        OnChange(nameof(Serial));
        OnChange(nameof(Session));
    }
}