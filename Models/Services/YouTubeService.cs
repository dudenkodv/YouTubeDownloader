using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Serilog;
using System.Globalization;
using System.IO;
using YouTubeDownloader.Models.DTOs;
using YouTubeDownloader.Models.Entities;
using YouTubeDownloader.Models.Extensions;
using YouTubeDownloader.Models.Interfaces;
using YouTubeDownloader.Models.Services.Arguments;
using YouTubeDownloader.Models.Settings;

namespace YouTubeDownloader.Models.Services;

public class YouTubeService : IYouTubeService {
    private readonly string _ytDlpPath;
    private readonly IProcessExecutor _executor;
    private readonly IYtDlpOutputParser _parser;
    private readonly IFfmpegConverter _converter;
    private readonly ITempFileManagerFactory _tempFileManagerFactory;
    private readonly ILogger<YouTubeService> _logger;
    private string _currentTitle = string.Empty;

    //public YouTubeService(
    //    IProcessExecutor executor,
    //    IYtDlpOutputParser parser,
    //    IFfmpegConverter converter,
    //    ITempFileManagerFactory tempFileManagerFactory,
    //    ILogger<YouTubeService> logger) {
    //    var baseDir = AppDomain.CurrentDomain.BaseDirectory;
    //    _ytDlpPath = Path.Combine(baseDir, "Tools", "yt-dlp.exe");
    //    _executor = executor;
    //    _parser = parser;
    //    _converter = converter;
    //    _tempFileManagerFactory = tempFileManagerFactory;
    //    _logger = logger;
    //}
    //переделать на DI
    //public YouTubeService() {
    //    var baseDir = AppDomain.CurrentDomain.BaseDirectory;
    //    _ytDlpPath = Path.Combine(baseDir, "Tools", "yt-dlp.exe");

    //    _executor = new ProcessExecutor();
    //    _parser = new YtDlpOutputParser();
    //    _tempFileManagerFactory = new TempFileManagerFactory();

    //    var loggerFactory = LoggerFactory.Create(builder => builder.AddSerilog(Log.Logger, dispose: true));
    //    _logger = loggerFactory.CreateLogger<YouTubeService>();
    //    _converter = new FfmpegConverter(_executor, _logger);
    //}
    public YouTubeService(IProcessExecutor executor,
    IYtDlpOutputParser parser,
    IFfmpegConverter converter,
    ITempFileManagerFactory tempFileManagerFactory,
    ILogger<YouTubeService> logger) {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        _ytDlpPath = Path.Combine(baseDir, "Tools", "yt-dlp.exe");
        _executor = executor;
        _parser = parser;
        _converter = converter;
        _tempFileManagerFactory = tempFileManagerFactory;
        _logger = logger;
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
            if (result.ExitCode != 0) {
                var errorMsg = $"Ошибка yt-dlp: {result.StandardError}";
                Log.Error(errorMsg);
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
            Log.Error(ex, "GetFormatsAsync error");
            return new ResultDto<List<VideoFormat>>(false, ex.Message, null!);
        }
    }

    public string GetVideoTitle() => _currentTitle;

    public async Task<ResultDto<bool>> DownloadAsync(string url, string formatId, string outputPath, string fileName,
        IProgress<double>? progress = null,
        IProgress<string>? status = null,
        CancellationToken cancellationToken = default) {
        using var tempManager = _tempFileManagerFactory.Create();
        try {
            var outputTemplate = tempManager.GetOutputTemplate(fileName);

            var result = await DownloadMainAsync(url, formatId, outputTemplate, progress, status);

            if (!result) {
                result = await DownloadSimpleAsync(url);

                if (!result) {
                    return new ResultDto<bool>(false, "Ошибка при скачивании", false);
                }
            }

            var downloadedFile = tempManager.GetFirstFile();

            if (string.IsNullOrEmpty(downloadedFile) || !File.Exists(downloadedFile)) {
                return new ResultDto<bool>(false, "Файл не найден после загрузки", false);
            }

            await ProcessDownloadedFileAsync(downloadedFile, outputPath, fileName, progress, status);

            return new ResultDto<bool>(true, "Загрузка завершена", true);
        } catch (OperationCanceledException) {
            Log.Information("Download cancelled");
            return new ResultDto<bool>(false, "Загрузка отменена", false);
        } catch (Exception ex) {
            Log.Error(ex, "Download error");
            return new ResultDto<bool>(false, ex.Message, false);
        }
    }

    private async Task<bool> DownloadMainAsync(string url, string formatId, string outputTemplate,
        IProgress<double>? progress = null, IProgress<string>? status = null) {
        try {
            var args = CommandBuilder.Create()
                .Verbose()
                .Format(formatId)
                .Output(outputTemplate)
                .NoWarnings()
                .Newline()
                .Progress()
                .Url(url)
                .Build();

            status?.Report("Скачивание видео...");

            string? downloadedFile = null;
            var lastPercent = 0;

            await _executor.ExecuteStreamingAsync(_ytDlpPath, args,
                onStdOut: line => {
                    var percent = _parser.ParsePercent(line);
                    if (percent.HasValue && (int)percent.Value != lastPercent) {
                        lastPercent = (int)percent.Value;
                        progress?.Report(percent.Value);
                    }
                },
                onStdErr: line => {
                    var percent = _parser.ParsePercent(line);
                    if (percent.HasValue && (int)percent.Value != lastPercent) {
                        lastPercent = (int)percent.Value;
                        progress?.Report(percent.Value);
                    }

                    var dest = _parser.ParseDestination(line);
                    if (!string.IsNullOrEmpty(dest))
                        downloadedFile = dest;
                });

            return !string.IsNullOrEmpty(downloadedFile);
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception ex) {
            Log.Error(ex, "DownloadMainAsync error");
            return false;
        }
    }

    private async Task<bool> DownloadSimpleAsync(string url) {
        try {
            var args = CommandBuilder.Create()
                .Verbose()
                .Url(url)
                .Build();

            await _executor.ExecuteAsync(_ytDlpPath, args);
            return true;
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception ex) {
            Log.Error(ex, "DownloadSimpleAsync error");
            return false;
        }
    }

    private async Task ProcessDownloadedFileAsync(string downloadedFile, string outputPath, string fileName,
        IProgress<double>? progress = null, IProgress<string>? status = null) {
        var extension = Path.GetExtension(downloadedFile).ToLower();

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
            status?.Report($"Сохранено: {Path.GetFileName(finalPath)}");
        }

        progress?.Report(100);
    }

    public void CancelDownload() {
        Log.Information("CancelDownload called");
        _executor.Cancel();
    }
}