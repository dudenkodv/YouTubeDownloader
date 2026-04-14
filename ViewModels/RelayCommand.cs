using System.Windows.Input;

namespace YouTubeDownloader.ViewModels;

public class RelayCommand : ICommand {
    private readonly Func<Task>? _asyncExecute;
    private readonly Action? _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null) {
        _execute = execute;
        _canExecute = canExecute;
    }

    public RelayCommand(Func<Task> asyncExecute, Func<bool>? canExecute = null) {
        _asyncExecute = asyncExecute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public async void Execute(object? parameter) {
        if (_asyncExecute != null)
            await _asyncExecute();
        else
            _execute?.Invoke();
    }

    public event EventHandler? CanExecuteChanged {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
}