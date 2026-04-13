using Microsoft.Win32;
using Serilog;
using System.Diagnostics;
using System.IO;
using System.Windows;
using YouTubeDownloader.Interfaces;
using YouTubeDownloader.Models;
using YouTubeDownloader.Services;

namespace YouTubeDownloader;

public partial class MainWindow : Window {
    private readonly IYouTubeService _youtubeService;
    private List<VideoFormat> _formats = new();
    private bool _isVideoMode = true;
    private CancellationTokenSource? _cts;
    private string? _lastDownloadedPath;
    private bool _isLoadingInfo = false;

    public MainWindow() {
        InitializeComponent();
        _youtubeService = new YouTubeService();
        Log.Information("Главное окно создано");
    }

    private async void LoadButton_Click(object sender, RoutedEventArgs e) {
        var url = UrlTextBox.Text.Trim();

        if (string.IsNullOrEmpty(url)) {
            MessageBox.Show("Введите ссылку", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _cts = new CancellationTokenSource();
        _isLoadingInfo = true;

        LoadButton.IsEnabled = false;
        CancelButton.IsEnabled = true;
        ShowSpinner("Получение информации...");

        try {
            _formats = await _youtubeService.GetFormatsAsync(url,
                new Progress<string>(msg => StatusText.Text = msg),
                _cts.Token);

            UpdateFormatsList();
            DownloadButton.IsEnabled = _formats.Any();
            HideSpinner();
        } catch (OperationCanceledException) {
            StatusText.Text = "Отменено";
            HideSpinner();
        } catch (Exception ex) {
            HideSpinner();
            Log.Error(ex, "Ошибка загрузки информации");
            MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        } finally {
            _isLoadingInfo = false;
            LoadButton.IsEnabled = true;
            CancelButton.IsEnabled = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void MediaType_Changed(object sender, RoutedEventArgs e) {
        _isVideoMode = VideoRadio.IsChecked == true;
        UpdateFormatsList();
    }

    private void UpdateFormatsList() {
        if (FormatsListBox == null)
            return;

        var filtered = _isVideoMode
            ? _formats.Where(f => !f.IsAudioOnly).ToList()
            : _formats.Where(f => f.IsAudioOnly).ToList();

        FormatsListBox.ItemsSource = filtered;
        FormatsListBox.SelectedIndex = filtered.Any() ? 0 : -1;
    }

    private async void DownloadButton_Click(object sender, RoutedEventArgs e) {
        var selectedFormat = FormatsListBox.SelectedItem as VideoFormat;

        if (selectedFormat == null) {
            MessageBox.Show("Выберите формат", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var originalFileName = await _youtubeService.GetVideoTitleAsync();

        var saveDialog = new SaveFileDialog {
            Title = "Сохранить как",
            Filter = _isVideoMode ? "MP4 файлы (*.mp4)|*.mp4" : "MP3 файлы (*.mp3)|*.mp3",
            FileName = originalFileName
        };

        if (saveDialog.ShowDialog() != true)
            return;

        _cts = new CancellationTokenSource();

        DownloadButton.IsEnabled = false;
        LoadButton.IsEnabled = false;
        CancelButton.IsEnabled = true;
        OpenFolderButton.IsEnabled = false;

        ShowSpinner("Загрузка...");
        ProgressBar.Visibility = Visibility.Visible;
        ProgressBar.Value = 0;

        try {
            var directory = Path.GetDirectoryName(saveDialog.FileName) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
            var fileName = Path.GetFileNameWithoutExtension(saveDialog.FileName);

            await _youtubeService.DownloadAsync(
                UrlTextBox.Text.Trim(),
                selectedFormat.FormatId,
                directory,
                fileName,
                new Progress<double>(p => {
                    ProgressBar.Value = p;
                    if (p > 0) {
                        ProgressBar.Visibility = Visibility.Visible;
                        SpinnerText.Text = $"Загрузка: {p:F0}%";
                    }
                }),
                new Progress<string>(s => StatusText.Text = s),
                _cts.Token);

            _lastDownloadedPath = saveDialog.FileName;

            HideSpinner();
            ProgressBar.Visibility = Visibility.Collapsed;

            var result = MessageBox.Show(
                $"Загрузка завершена!\n{Path.GetFileName(saveDialog.FileName)}\n\nОткрыть папку с файлом?",
                "Готово",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes) {
                Process.Start("explorer.exe", $"/select,\"{saveDialog.FileName}\"");
            }

            StatusText.Text = "Готов";
            OpenFolderButton.IsEnabled = true;
        } catch (OperationCanceledException) {
            HideSpinner();
            ProgressBar.Visibility = Visibility.Collapsed;
            StatusText.Text = "Отменено";
        } catch (Exception ex) {
            HideSpinner();
            ProgressBar.Visibility = Visibility.Collapsed;
            Log.Error(ex, "Ошибка загрузки");
            MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText.Text = "Ошибка";
        } finally {
            _cts?.Dispose();
            _cts = null;
            DownloadButton.IsEnabled = true;
            LoadButton.IsEnabled = true;
            CancelButton.IsEnabled = false;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) {
        _cts?.Cancel();
        _youtubeService.CancelDownload();
        CancelButton.IsEnabled = false;
        StatusText.Text = "Отмена...";
    }

    private void OpenLogsButton_Click(object sender, RoutedEventArgs e) {
        var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");

        if (!Directory.Exists(logDir))
            Directory.CreateDirectory(logDir);

        try {
            Process.Start(new ProcessStartInfo {
                FileName = logDir,
                UseShellExecute = true
            });
        } catch (Exception ex) {
            MessageBox.Show($"Не удалось открыть папку: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ShowSpinner(string text) {
        SpinnerText.Text = text;
        SpinnerOverlay.Visibility = Visibility.Visible;
    }

    private void HideSpinner() {
        SpinnerOverlay.Visibility = Visibility.Collapsed;
    }

    private async void UpdateYes_Click(object sender, RoutedEventArgs e) { }
    private void UpdateNo_Click(object sender, RoutedEventArgs e) { }

    private void OpenFolderButton_Click(object sender, RoutedEventArgs e) {
        if (!string.IsNullOrEmpty(_lastDownloadedPath)) {
            var directory = Path.GetDirectoryName(_lastDownloadedPath);
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory)) {
                Process.Start("explorer.exe", directory);
            }
        }
    }

    private async void CheckUpdatesButton_Click(object sender, RoutedEventArgs e) {
        _cts = new CancellationTokenSource();

        CheckUpdatesButton.IsEnabled = false;
        CancelButton.IsEnabled = true;
        StatusText.Text = "Проверка обновлений...";

        try {
            var updateService = new UpdateService();
            var ytUpdate = await updateService.CheckYtDlpUpdateAsync();

            if (_cts.Token.IsCancellationRequested) {
                StatusText.Text = "Отменено";
                return;
            }

            if (ytUpdate.IsUpdateAvailable) {
                var result = MessageBox.Show(
                    $"Доступно обновление yt-dlp!\n\n" +
                    $"Текущая версия: {ytUpdate.CurrentVersion}\n" +
                    $"Новая версия: {ytUpdate.LatestVersion}\n\n" +
                    $"Обновить?",
                    "Обновление",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes) {
                    var progress = new Progress<int>(p => StatusText.Text = $"Загрузка обновления: {p}%");
                    await updateService.DownloadAndUpdateToolAsync(ytUpdate, progress);

                    if (!_cts.Token.IsCancellationRequested)
                        MessageBox.Show("Обновление установлено! Перезапустите приложение.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            } else {
                if (!_cts.Token.IsCancellationRequested)
                    MessageBox.Show($"Установлена последняя версия yt-dlp ({ytUpdate.CurrentVersion})", "Обновления не найдены", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            updateService.Dispose();
        } catch (OperationCanceledException) {
            StatusText.Text = "Отменено";
        } catch (Exception ex) {
            Log.Error(ex, "Ошибка проверки обновлений");
            MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        } finally {
            _cts?.Dispose();
            _cts = null;
            CheckUpdatesButton.IsEnabled = true;
            CancelButton.IsEnabled = false;
            if (!_isLoadingInfo)
                StatusText.Text = "Готов";
        }
    }
}