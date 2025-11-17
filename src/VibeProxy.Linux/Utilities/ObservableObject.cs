using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VibeProxy.Linux.Utilities;

public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
    {
        if (propertyName is null)
        {
            return;
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value) || propertyName is null)
        {
            return false;
        }

        field = value;
        RaisePropertyChanged(propertyName);
        return true;
    }
}
