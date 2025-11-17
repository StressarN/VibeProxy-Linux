using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace VibeProxy.Linux.Utilities;

public sealed class AsyncCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;

    public AsyncCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public async void Execute(object? parameter)
    {
        try
        {
            await _execute().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Avoid crashing the UI on unhandled async exceptions.
            Console.Error.WriteLine(ex);
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
