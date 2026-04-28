using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using YouTubeDownloader.Models.Entities;
using YouTubeDownloader.Models.Interfaces;
using YouTubeDownloader.Models.Interfaces.Factories;
using YouTubeDownloader.Models.ProgressTasks;
using YouTubeDownloader.Models.Services;

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
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<MainViewModel> _logger;
    private readonly IProgressTaskFactory _taskFactory;
    private bool _isSpinnerVisible;
    private string _spinnerText = "Загрузка...";
    private int cancelCount = 0;

    public MainViewModel(IYouTubeService youtubeService,
        ILoggerFactory loggerFactory,
        IProgressTaskFactory taskFactory) {
        _youtubeService = youtubeService;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<MainViewModel>();

        LoadCommand = new RelayCommand(LoadInfo, () => !IsLoading);
        AddToQueueCommand = new RelayCommand(AddToQueue, () => SelectedFormat != null && !IsLoading);
        CancelTaskCommand = new RelayCommand(Cancel);
        OpenLogsCommand = new RelayCommand(OpenLogs);
        OpenFolderCommand = new RelayCommand(OpenFolder, () => !string.IsNullOrEmpty(_lastDownloadedPath));
        CheckUpdatesCommand = new RelayCommand(CheckUpdatesAsync, () => !IsLoading);
        _taskFactory = taskFactory;
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
    public bool IsSpinnerVisible {
        get => _isSpinnerVisible;
        set { _isSpinnerVisible = value; OnPropertyChanged(); }
    }

    public string SpinnerText {
        get => _spinnerText;
        set { _spinnerText = value; OnPropertyChanged(); }
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

        var task = _taskFactory.Create(new TaskParameters {
            Type = TaskType.LoadInfo,
            Url = Url,
            OnFormatsLoaded = (formats) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Formats.Clear();
                    foreach (var format in formats)
                        Formats.Add(format);
                    UpdateFormatsList();
                    IsLoading = false;
                    StatusText = "Готов";
                });
            }
        });

        ActiveTasks.Add(task);
        await RunTaskAsync(task);
    }

    private async void AddToQueue() {
        if (SelectedFormat == null)
            return;

        var videoTitle = _youtubeService.GetVideoTitle() ?? "video";
        var fileName = $"{videoTitle}_{SelectedFormat.DisplayName}";

        var saveDialog = new Microsoft.Win32.SaveFileDialog {
            Title = "Сохранить как",
            Filter = IsVideoMode ? "MP4 файлы (*.mp4)|*.mp4" : "MP3 файлы (*.mp3)|*.mp3",
            FileName = fileName
        };

        if (saveDialog.ShowDialog() != true)
            return;

        var task = _taskFactory.Create(new TaskParameters {
            Type = TaskType.Download,
            Url = Url,
            FormatId = SelectedFormat.FormatId,
            OutputPath = saveDialog.FileName,
            Name = videoTitle,
            IsVideoMode = IsVideoMode,
        });

        ActiveTasks.Add(task);
        _ = RunTaskAsync(task);
    }

    private async Task RunTaskAsync(ProgressTask task) {
        using var cts = new CancellationTokenSource();
        task.CancellationTokenSource = cts;  // ← ПЕРЕДАЁМ ССЫЛКУ

        _logger.LogInformation("RunTaskAsync: создан cts с HashCode: {HashCode}", cts.GetHashCode());
        _logger.LogInformation("  Задача: {TaskName}, Id: {TaskId}", task.Name, task.Id);

        var progress = new Progress<double>(p => task.Progress = p);
        var status = new Progress<string>(s => task.Status = s);

        try {
            await task.ExecuteAsync(progress, status, cts.Token);
        } catch (OperationCanceledException) {
            // статус уже установлен в task.Cancel()
            _logger.LogInformation($"MainViewModel.RunTaskAsync cancel");
        } catch (Exception ex) {
            _logger.LogError(ex, "Ошибка выполнения задачи {TaskName}", task.Name);
            task.Status = $"Ошибка: {ex.Message}";
        } finally {
            task.IsActive = false;
            task.CancellationTokenSource = null;  // ← ОЧИЩАЕМ
            if (task is DownloadTask downloadTask && downloadTask.Status == "Завершено") {
                _lastDownloadedPath = downloadTask.OutputPath;
            }
            if (task is LoadInfoTask) {
                IsLoading = false;
                StatusText = "Готов";
            }
        }
    }

    private async Task CancelTask(object? parameter) {
        //todo при отмене надо удалять временную папку или файл
        cancelCount++;
        _logger.LogInformation("CancelTask start. cancelCount = {CancelCount}", cancelCount);

        if (parameter is not ProgressTask task || !task.IsActive)
            return;

        // Показываем спиннер на UI
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            IsSpinnerVisible = true;
            SpinnerText = "Отмена задачи...";
        });

        try {
            _logger.LogInformation("CancelTask: вызываем task.Cancel()");

            // Отменяем задачу в фоне, не блокируя UI
            await Task.Run(() => task.Cancel());

            _logger.LogInformation("CancelTask: task.Cancel() выполнен");

            // Даём 1 секунду на graceful отмену
            await Task.Delay(1000);

            // Если задача всё ещё активна, ждём с таймаутом
            if (task.IsActive) {
                _logger.LogInformation("CancelTask: ожидаем завершения задачи...");
                var timeout = DateTime.Now.AddSeconds(5);
                while (task.IsActive && DateTime.Now < timeout) {
                    await Task.Delay(100);
                }

                if (task.IsActive)
                    _logger.LogWarning("CancelTask: таймаут ожидания отмены задачи");
            }
        } finally {
            // Скрываем спиннер
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                IsSpinnerVisible = false;
            });

            _logger.LogInformation("CancelTask завершён. cancelCount = {CancelCount}", cancelCount);
        }
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
    private void Cancel(object? param) {
        _logger.LogInformation("CancelTaskCommand вызван. Parameter: {Parameter}", param);
        CancelTask(param);
    }

    private async Task CheckUpdatesAsync() {
        using var updateService = new UpdateService(_loggerFactory.CreateLogger<UpdateService>());
        StatusText = "Проверка обновлений...";

        try {
            var result = await updateService.CheckYtDlpUpdateAsync();
            if (!result.isSuccess) {
                MessageBox.Show($"Ошибка: {result.message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var ytUpdate = result.data;
            if (ytUpdate.IsUpdateAvailable) {
                var dialogResult = MessageBox.Show(
                    $"Доступно обновление yt-dlp!\n\nТекущая: {ytUpdate.CurrentVersion}\nНовая: {ytUpdate.LatestVersion}\n\nОбновить?",
                    "Обновление",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (dialogResult == MessageBoxResult.Yes) {
                    var progress = new Progress<int>(p => StatusText = $"Загрузка обновления: {p}%");
                    var downloadResult = await updateService.DownloadAndUpdateToolAsync(ytUpdate, progress);

                    if (downloadResult.isSuccess) {
                        MessageBox.Show("Обновление установлено! Перезапустите приложение.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
                    } else {
                        MessageBox.Show($"Ошибка обновления: {downloadResult.message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            } else {
                MessageBox.Show($"Установлена последняя версия yt-dlp ({ytUpdate.CurrentVersion})", "Обновлений нет", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        } catch (Exception ex) {
            _logger.LogError(ex, "Ошибка проверки обновлений");
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