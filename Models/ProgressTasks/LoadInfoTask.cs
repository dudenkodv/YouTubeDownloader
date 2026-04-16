using Serilog;
using System.Xml.Linq;
using YouTubeDownloader.Models.Entities;
using YouTubeDownloader.Models.Interfaces;

namespace YouTubeDownloader.Models.ProgressTasks;

public class LoadInfoTask : ProgressTask {
    private readonly IYouTubeService _youtubeService;
    private readonly string _url;
    private readonly Action<List<VideoFormat>> _onFormatsLoaded;

    public LoadInfoTask(IYouTubeService youtubeService, string url, Action<List<VideoFormat>> onFormatsLoaded) {
        _youtubeService = youtubeService;
        _url = url;
        _onFormatsLoaded = onFormatsLoaded;
        Name = "Загрузка информации о видео";
    }

    public override async Task ExecuteAsync(IProgress<double> progress, IProgress<string> status, CancellationToken cancellationToken) {
        try {
            status?.Report("Получение информации...");
            progress?.Report(0);

            var result = await _youtubeService.GetFormatsAsync(_url, status, cancellationToken);
            if (!result.isSuccess) {
                Status = result.message;
                return;
            }
            var formats = result.data;

            _onFormatsLoaded(formats);

            Status = "Завершено";
            Progress = 100;
        } catch (OperationCanceledException) {
            Status = "Отменено";
            throw;
        } catch (Exception ex) {
            Log.Error(ex, "Ошибка загрузки информации");
            Status = $"Ошибка: {ex.Message}";
            throw;
        }
    }
}