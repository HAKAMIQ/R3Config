using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace R3Config.Vm;

public abstract class VmBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Set<T>(
        ref T storage,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        storage = value;
        OnChange(propertyName);
        return true;
    }

    protected void OnChange([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}