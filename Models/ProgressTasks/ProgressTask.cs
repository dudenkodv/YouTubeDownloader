using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace YouTubeDownloader.Models.ProgressTasks;

public abstract class ProgressTask : INotifyPropertyChanged {
    private string _id = string.Empty;
    private string _name = string.Empty;
    private string _status = "Ожидание";
    private double _progress;
    private bool _isActive = true;
    private CancellationTokenSource? _cts;

    public string Id {
        get => _id;
        set { _id = value; OnPropertyChanged(); }
    }

    public string Name {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public string Status {
        get => _status;
        set { _status = value; OnPropertyChanged(); }
    }

    public double Progress {
        get => _progress;
        set { _progress = value; OnPropertyChanged(); }
    }

    public bool IsActive {
        get => _isActive;
        set { _isActive = value; OnPropertyChanged(); }
    }

    public CancellationToken CancellationToken => _cts?.Token ?? CancellationToken.None;

    public CancellationTokenSource CreateCancellationTokenSource() {
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        return _cts;
    }

    public void Cancel() {
        _cts?.Cancel();
        Status = "Отменено";
    }

    public void Dispose() {
        _cts?.Dispose();
    }

    public abstract Task ExecuteAsync(IProgress<double> progress, IProgress<string> status, CancellationToken cancellationToken);

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}