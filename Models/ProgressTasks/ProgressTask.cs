using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using YouTubeDownloader.Models.Interfaces;

namespace YouTubeDownloader.Models.ProgressTasks;

public abstract class ProgressTask : IProgressTask, INotifyPropertyChanged {
    private string _id = string.Empty;
    private string _name = string.Empty;
    private string _status = "Ожидание";
    private double _progress;
    private bool _isActive = true;
    private CancellationTokenSource? _cts;
    protected readonly ILogger _logger;

    protected ProgressTask(ILogger logger) {
        _logger = logger;
        _id = Guid.NewGuid().ToString();
    }

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


    public CancellationTokenSource? CancellationTokenSource {
        get => _cts;
        set {
            _cts?.Dispose();
            _cts = value;
        }
    }

    public void Cancel() {
        _logger.LogInformation("Cancel() вызван для задачи {TaskName}, Id: {TaskId}", Name, Id);
        _logger.LogInformation("  _cts is null: {IsNull}, HashCode: {HashCode}",
            _cts is null, _cts?.GetHashCode());
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