using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace R3Config.Vm;

public sealed class RelayCmd : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canRun;

    public RelayCmd(Action execute, Func<bool>? canExecute = null)
    {
        ArgumentNullException.ThrowIfNull(execute);

        _execute = execute;
        _canRun = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    bool ICommand.CanExecute(object? arg) => CanRun(arg);

    public bool CanRun(object? parameter)
    {
        return _canRun?.Invoke() ?? true;
    }

    public void Execute(object? parameter)
    {
        _execute();
    }

    public void RaiseCan()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}

public sealed class AsyncCmd : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canRun;
    private bool _isRun;

    public AsyncCmd(Func<Task> execute, Func<bool>? canExecute = null)
    {
        ArgumentNullException.ThrowIfNull(execute);

        _execute = execute;
        _canRun = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    bool ICommand.CanExecute(object? arg) => CanRun(arg);

    public bool CanRun(object? parameter)
    {
        return !_isRun && (_canRun?.Invoke() ?? true);
    }

    public async void Execute(object? parameter)
    {
        if (!CanRun(parameter))
        {
            return;
        }

        try
        {
            _isRun = true;
            RaiseCan();

            await _execute();
        }
        finally
        {
            _isRun = false;
            RaiseCan();
        }
    }

    public void RaiseCan()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
