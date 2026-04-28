using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.IO;
using YouTubeDownloader.Models.DTOs;
using YouTubeDownloader.Models.Entities;
using YouTubeDownloader.Models.Extensions;
using YouTubeDownloader.Models.Interfaces;
using YouTubeDownloader.Models.Interfaces.Factories;
using YouTubeDownloader.Models.Services.Arguments;

namespace YouTubeDownloader.Models.Services;

public class YouTubeService : IYouTubeService {
    private readonly string _ytDlpPath;
    private readonly IProcessExecutor _executor;
    private readonly IYtDlpOutputParser _parser;
    private readonly IFfmpegConverter _converter;
    private readonly ITempFileManagerFactory _tempFileManagerFactory;
    private readonly ILogger<YouTubeService> _logger;
    private readonly IDownloadStrategy _formatStrategy;
    private readonly IDownloadStrategy _simpleStrategy;
    private string _currentTitle = string.Empty;
    public YouTubeService(IProcessExecutorFactory executorFactory,
    IYtDlpOutputParser parser,
    IFfmpegConverter converter,
    ITempFileManagerFactory tempFileManagerFactory,
    ILogger<YouTubeService> logger,
    IDownloadStrategyFactory strategyFactory) {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        _ytDlpPath = Path.Combine(baseDir, "Tools", "yt-dlp.exe");
        _executor = executorFactory.Create(ProcessExecutorTypeEnum.Killable);
        _parser = parser;
        _converter = converter;
        _tempFileManagerFactory = tempFileManagerFactory;
        _logger = logger;
        _formatStrategy = strategyFactory.Create(DownloadStrategyTypeEnum.Format);
        _simpleStrategy = strategyFactory.Create(DownloadStrategyTypeEnum.Simple);
    }

    public async Task<ResultDto<List<VideoFormat>>> GetFormatsAsync(string url, IProgress<string>? progress = null, CancellationToken cancellationToken = default) {
        try {
            progress?.Report("Получение информации о видео...");
            cancellationToken.ThrowIfCancellationRequested();

            var args = CommandBuilder.Create()
                .Verbose()
                .Json()
                .NoWarnings()
                .Url(url)
                .Build();

            var result = await _executor.ExecuteAsync(_ytDlpPath, args, cancellationToken);
            if (cancellationToken.IsCancellationRequested) {
                _logger.LogInformation("Операция отменена пользователем");
                return new ResultDto<List<VideoFormat>>(false, "Операция отменена по требования пользователя", null!);
            }
            if (result.ExitCode != 0) {
                var errorMsg = $"Ошибка yt-dlp: {result.StandardError}";
                _logger.LogError(errorMsg);
                return new ResultDto<List<VideoFormat>>(false, errorMsg, null!);
            }

            var json = JObject.Parse(result.StandardOutput);
            _currentTitle = json["title"]?.ToString() ?? "Unknown";

            var formatsArray = json["formats"] as JArray;
            if (formatsArray == null) {
                return new ResultDto<List<VideoFormat>>(true, "Форматы не найдены", new List<VideoFormat>());
            }

            var formats = formatsArray
                .Select(fmt => fmt.ToVideoFormat())
                .Where(f => f != null && !string.IsNullOrEmpty(f.FormatId))
                .Cast<VideoFormat>()
                .ToList();

            var videoFormats = formats.GetVideoOnlyFormats();
            var audioFormats = formats.GetAudioFormats();

            var combinedFormats = videoFormats
                .Select(video => new {
                    Video = video,
                    BestAudio = audioFormats.FirstOrDefault()
                })
                .Where(x => x.BestAudio != null)
                .Select(x => new VideoFormat {
                    FormatId = $"{x.Video.FormatId}+{x.BestAudio!.FormatId}",
                    Extension = "mp4",
                    Resolution = x.Video.Resolution,
                    Height = x.Video.Height,
                    FormatNote = $"{x.Video.FormatNote}+{x.BestAudio.FormatNote}",
                    IsAudioOnly = false,
                    Vcodec = x.Video.Vcodec,
                    Acodec = x.BestAudio.Acodec,
                    Tbr = (x.Video.Tbr ?? 0) + (x.BestAudio.Tbr ?? 0),
                    Filesize = (x.Video.Filesize ?? 0) + (x.BestAudio.Filesize ?? 0)
                })
                .ToList();

            var fullFormats = formats
                .Where(f => !f.IsAudioOnly && f.Acodec != "none")
                .ToList();

            var resultFormats = combinedFormats
                .Concat(fullFormats)
                .Concat(audioFormats)
                .ToList();

            var finalFormats = resultFormats
                .DistinctBy(i => i.DisplayName)
                .OrderBy(i => i.DisplayName)
                .ToList();

            progress?.Report($"Готово: {_currentTitle}");
            return new ResultDto<List<VideoFormat>>(true, "Успешно", finalFormats);
        } catch (OperationCanceledException) {
            return new ResultDto<List<VideoFormat>>(false, "Операция отменена", null!);
        } catch (Exception ex) {
            _logger.LogError(ex, "GetFormatsAsync error");
            return new ResultDto<List<VideoFormat>>(false, ex.Message, null!);
        }
    }

    public string GetVideoTitle() => _currentTitle;

    public async Task<ResultDto<bool>> DownloadAsync(string url, string formatId, string outputPath, string fileName, bool isVideoMode,
        IProgress<double>? progress = null,
        IProgress<string>? status = null,
        CancellationToken cancellationToken = default) {
        _logger.LogInformation("YouTubeService DownloadAsync получил cancellationToken с HashCode: {HashCode}", cancellationToken.GetHashCode());
        using var tempManager = _tempFileManagerFactory.Create();
        try {
            var outputTemplate = tempManager.GetOutputTemplate(fileName);

            var downloadType = (DownloadTypeEnum)(Convert.ToInt32(isVideoMode));
            var downloadedFile = await _formatStrategy.ExecuteAsync(_ytDlpPath, url, formatId, outputTemplate, progress, status, cancellationToken, downloadType);
            if (string.IsNullOrEmpty(downloadedFile)) {
                _logger.LogInformation(" _simpleStrategy.ExecuteAsync start");
                downloadedFile = await _simpleStrategy.ExecuteAsync(_ytDlpPath, url, formatId, outputTemplate, progress, status, cancellationToken, downloadType);
            }
            if (string.IsNullOrEmpty(downloadedFile) || !File.Exists(downloadedFile)) {
                return new ResultDto<bool>(false, "Файл не найден после загрузки", false);
            }

            await ProcessDownloadedFileAsync(downloadedFile, outputPath, fileName, progress, status);

            return new ResultDto<bool>(true, "Загрузка завершена", true);
        } catch (OperationCanceledException) {
            _logger.LogInformation("YouTubeService.DownloadAsync отменён для {FileName}", fileName);
            return new ResultDto<bool>(false, "Загрузка отменена", false);
        } catch (Exception ex) {
            _logger.LogError(ex, "Download error");
            return new ResultDto<bool>(false, ex.Message, false);
        }
    }

    private async Task ProcessDownloadedFileAsync(string downloadedFile, string outputPath, string fileName,
        IProgress<double>? progress = null, 
        IProgress<string>? status = null) {
        var extension = Path.GetExtension(downloadedFile).ToLower();

        var msg = $"ProcessDownloadedFileAsync start. extension = {extension}";
        _logger.LogInformation(msg);

        if (extension == ".m4a") {
            status?.Report("Конвертация в MP3...");
            var finalPath = Path.Combine(outputPath, fileName + ".mp3");
            await _converter.ConvertToMp3Async(downloadedFile, finalPath);
            File.Delete(downloadedFile);
            status?.Report($"Сохранено: {Path.GetFileName(finalPath)}");
        } else {
            status?.Report("Сохранение файла...");
            var finalPath = Path.Combine(outputPath, fileName + extension);
            if (File.Exists(finalPath))
                File.Delete(finalPath);
            File.Move(downloadedFile, finalPath);
            _logger.LogInformation($"downloadedFile = {downloadedFile}");
            _logger.LogInformation($"finalPath = {finalPath}");
            status?.Report($"Сохранено: {Path.GetFileName(finalPath)}");
        }
        msg = $"ProcessDownloadedFileAsync end. extension = {extension}";
        _logger.LogInformation(msg);

        progress?.Report(100);
    }

    public void CancelDownload() {
        _logger.LogInformation("CancelDownload called");
        _executor.Cancel();
    }
}