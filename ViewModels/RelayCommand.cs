using System.Windows.Input;

namespace YouTubeDownloader.ViewModels;

public class RelayCommand : ICommand {
    private readonly Func<object?, Task>? _asyncExecute;
    private readonly Action<object?>? _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null) {
        _execute = execute;
        _canExecute = canExecute;
    }

    public RelayCommand(Func<object?, Task> asyncExecute, Func<object?, bool>? canExecute = null) {
        _asyncExecute = asyncExecute;
        _canExecute = canExecute;
    }

    public RelayCommand(Action execute, Func<bool>? canExecute = null) {
        _execute = _ => execute();
        _canExecute = _ => canExecute?.Invoke() ?? true;
    }

    public RelayCommand(Func<Task> asyncExecute, Func<bool>? canExecute = null) {
        _asyncExecute = _ => asyncExecute();
        _canExecute = _ => canExecute?.Invoke() ?? true;
    }

    public bool CanExecute(object? parameter) {
        return _canExecute?.Invoke(parameter) ?? true;
    }

    public async void Execute(object? parameter) {
        if (_asyncExecute != null)
            await _asyncExecute(parameter);
        else
            _execute?.Invoke(parameter);
    }

    public event EventHandler? CanExecuteChanged {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
}