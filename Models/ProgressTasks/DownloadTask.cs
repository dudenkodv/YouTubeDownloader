using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.IO;
using System.Windows;
using YouTubeDownloader.Models.Interfaces;

namespace YouTubeDownloader.Models.ProgressTasks;

public class DownloadTask : ProgressTask {
    private readonly IYouTubeService _youtubeService;
    private readonly ILogger<DownloadTask> _logger;

    public string Url { get; set; } = string.Empty;
    public string FormatId { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;

    public DownloadTask(IYouTubeService youtubeService, ILogger<DownloadTask> logger) {
        _youtubeService = youtubeService;
        Id = Guid.NewGuid().ToString();
        _logger = logger;
    }

    public override async Task ExecuteAsync(IProgress<double> progress, IProgress<string> status, CancellationToken cancellationToken) {
        try {
            var directory = Path.GetDirectoryName(OutputPath) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
            var fileName = Path.GetFileNameWithoutExtension(OutputPath);

            var result = await _youtubeService.DownloadAsync(Url, FormatId, directory, fileName, progress, status, cancellationToken);
            if (!result.isSuccess) {
                Status = result.message;
                return;
            }

            Status = "Завершено";
            Progress = 100;

            Application.Current.Dispatcher.Invoke(() =>
            {
                var result = MessageBox.Show(
                    $"Загрузка завершена!\n{Path.GetFileName(OutputPath)}\n\nОткрыть папку с файлом?",
                    "Готово",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes) {
                    Process.Start("explorer.exe", $"/select,\"{OutputPath}\"");
                }
            });
        } catch (OperationCanceledException) {
            Status = "Отменено";
            throw;
        } catch (Exception ex) {
            _logger.LogError(ex, "Ошибка загрузки");
            Status = $"Ошибка: {ex.Message}";
            throw;
        }
    }
}