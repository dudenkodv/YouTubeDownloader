using Serilog;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using YouTubeDownloader.Models.Entities;
using YouTubeDownloader.Models.Interfaces;
using YouTubeDownloader.Models.Services;
using YouTubeDownloader.Models.ProgressTasks;

namespace YouTubeDownloader.ViewModels;

public class MainViewModel : ViewModelBase {
    private readonly IYouTubeService _youtubeService;
    private string _url = "https://www.youtube.com/watch?v=YWRw_fTrh9s";
    private ObservableCollection<VideoFormat> _formats = new();
    private VideoFormat? _selectedFormat;
    private bool _isVideoMode = true;
    private bool _isLoading;
    private string _statusText = "Готов";
    private string? _lastDownloadedPath;
    private CancellationTokenSource? _cts;
    private ObservableCollection<ProgressTask> _activeTasks = new();

    public MainViewModel() {
        _youtubeService = new YouTubeService();

        LoadCommand = new RelayCommand(LoadInfo, () => !IsLoading);
        AddToQueueCommand = new RelayCommand(AddToQueue, () => SelectedFormat != null && !IsLoading);
        CancelTaskCommand = new RelayCommand((object? param) => CancelTask(param), (object? param) => true);
        OpenLogsCommand = new RelayCommand(OpenLogs);
        OpenFolderCommand = new RelayCommand(OpenFolder, () => !string.IsNullOrEmpty(_lastDownloadedPath));
        CheckUpdatesCommand = new RelayCommand(async () => await CheckUpdatesAsync(), () => !IsLoading);
    }

    public string Url {
        get => _url;
        set { _url = value; OnPropertyChanged(); }
    }

    public ObservableCollection<VideoFormat> Formats {
        get => _formats;
        set { _formats = value; OnPropertyChanged(); }
    }

    public VideoFormat? SelectedFormat {
        get => _selectedFormat;
        set { _selectedFormat = value; OnPropertyChanged(); }
    }

    public bool IsVideoMode {
        get => _isVideoMode;
        set { _isVideoMode = value; OnPropertyChanged(); UpdateFormatsList(); }
    }

    public bool IsAudioMode {
        get => !_isVideoMode;
        set { _isVideoMode = !value; OnPropertyChanged(nameof(IsVideoMode)); UpdateFormatsList(); }
    }

    public bool IsLoading {
        get => _isLoading;
        set { _isLoading = value; OnPropertyChanged(); }
    }

    public string StatusText {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    public ObservableCollection<ProgressTask> ActiveTasks {
        get => _activeTasks;
        set { _activeTasks = value; OnPropertyChanged(); }
    }

    public ICommand LoadCommand { get; }
    public ICommand AddToQueueCommand { get; }
    public ICommand CancelTaskCommand { get; }
    public ICommand OpenLogsCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public ICommand CheckUpdatesCommand { get; }

    private async void LoadInfo() {
        if (string.IsNullOrEmpty(Url))
            return;

        IsLoading = true;
        StatusText = "Загрузка информации...";

        var task = new LoadInfoTask(_youtubeService, Url, (formats) => {
            Application.Current.Dispatcher.Invoke(() => {
                Formats.Clear();
                foreach (var format in formats)
                    Formats.Add(format);
                UpdateFormatsList();
                IsLoading = false;
                StatusText = "Готов";
            });
        });

        ActiveTasks.Add(task);
        await RunTaskAsync(task);
    }

    private async void AddToQueue() {
        if (SelectedFormat == null)
            return;

        var videoTitle = await _youtubeService.GetVideoTitleAsync() ?? "video";
        var fileName = $"{videoTitle}_{SelectedFormat.DisplayName}";

        var saveDialog = new Microsoft.Win32.SaveFileDialog {
            Title = "Сохранить как",
            Filter = IsVideoMode ? "MP4 файлы (*.mp4)|*.mp4" : "MP3 файлы (*.mp3)|*.mp3",
            FileName = fileName
        };

        if (saveDialog.ShowDialog() != true)
            return;

        var task = new DownloadTask(_youtubeService) {
            Name = videoTitle,
            Url = Url,
            FormatId = SelectedFormat.FormatId,
            OutputPath = saveDialog.FileName,
            Status = "Ожидание"
        };

        ActiveTasks.Add(task);
        _ = RunTaskAsync(task);
    }

    private async Task RunTaskAsync(ProgressTask task) {
        using var cts = task.CreateCancellationTokenSource();

        var progress = new Progress<double>(p => task.Progress = p);
        var status = new Progress<string>(s => task.Status = s);

        try {
            await task.ExecuteAsync(progress, status, cts.Token);
        } catch (OperationCanceledException) {
            // статус уже установлен в task.Cancel()
        } catch (Exception ex) {
            Log.Error(ex, "Ошибка выполнения задачи {TaskName}", task.Name);
            task.Status = $"Ошибка: {ex.Message}";
        } finally {
            task.IsActive = false;
            if (task is DownloadTask downloadTask && downloadTask.Status == "Завершено") {
                _lastDownloadedPath = downloadTask.OutputPath;
            }
        }
    }

    private void CancelTask(object? parameter) {
        if (parameter is ProgressTask task && task.IsActive)
            task.Cancel();
    }

    private void OpenLogs() {
        var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
        if (!Directory.Exists(logDir))
            Directory.CreateDirectory(logDir);

        Process.Start(new ProcessStartInfo { FileName = logDir, UseShellExecute = true });
    }

    private void OpenFolder() {
        if (!string.IsNullOrEmpty(_lastDownloadedPath)) {
            var directory = Path.GetDirectoryName(_lastDownloadedPath);
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                Process.Start("explorer.exe", directory);
        }
    }

    private async Task CheckUpdatesAsync() {
        using var updateService = new UpdateService();
        StatusText = "Проверка обновлений...";

        try {
            var ytUpdate = await updateService.CheckYtDlpUpdateAsync();
            if (ytUpdate.IsUpdateAvailable) {
                var result = MessageBox.Show(
                    $"Доступно обновление yt-dlp!\n\nТекущая: {ytUpdate.CurrentVersion}\nНовая: {ytUpdate.LatestVersion}\n\nОбновить?",
                    "Обновление",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes) {
                    var progress = new Progress<int>(p => StatusText = $"Загрузка обновления: {p}%");
                    await updateService.DownloadAndUpdateToolAsync(ytUpdate, progress);
                    MessageBox.Show("Обновление установлено! Перезапустите приложение.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            } else {
                MessageBox.Show($"Установлена последняя версия yt-dlp ({ytUpdate.CurrentVersion})", "Обновлений нет", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        } catch (Exception ex) {
            Log.Error(ex, "Ошибка проверки обновлений");
            MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        } finally {
            StatusText = "Готов";
        }
    }

    private void UpdateFormatsList() {
        var filtered = IsVideoMode
            ? _formats.Where(f => !f.IsAudioOnly).ToList()
            : _formats.Where(f => f.IsAudioOnly).ToList();

        Formats.Clear();
        foreach (var format in filtered)
            Formats.Add(format);

        SelectedFormat = Formats.FirstOrDefault();
    }
}