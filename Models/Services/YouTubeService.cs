using System.Globalization;
using System.IO;
using Newtonsoft.Json.Linq;
using Serilog;
using YouTubeDownloader.Models.DTOs;
using YouTubeDownloader.Models.Entities;
using YouTubeDownloader.Models.Extensions;
using YouTubeDownloader.Models.Interfaces;
using YouTubeDownloader.Models.Services.Arguments;

namespace YouTubeDownloader.Models.Services;

public class YouTubeService : IYouTubeService {
    private readonly string _ytDlpPath;
    private readonly ProcessExecutor _executor;
    private readonly YtDlpOutputParser _parser;
    private readonly FfmpegConverter _converter;
    private readonly TempFileManager _tempManager;
    private string _currentTitle = string.Empty;

    public YouTubeService() {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        _ytDlpPath = Path.Combine(baseDir, "Tools", "yt-dlp.exe");
        _executor = new ProcessExecutor();
        _parser = new YtDlpOutputParser();
        _converter = new FfmpegConverter(_executor);
        _tempManager = new TempFileManager();
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

            Log.Debug("Arguments: {Args}", args);

            var result = await _executor.ExecuteAsync(_ytDlpPath, args, cancellationToken);

            if (!string.IsNullOrEmpty(result.StandardError))
                Log.Debug("STDERR: {Error}", result.StandardError);

            if (result.ExitCode != 0) {
                var errorMsg = $"Ошибка yt-dlp: {result.StandardError}";
                Log.Error(errorMsg);
                return new ResultDto<List<VideoFormat>>(false, errorMsg, null!);
            }

            var json = JObject.Parse(result.StandardOutput);
            _currentTitle = json["title"]?.ToString() ?? "Unknown";
            Log.Information("Title: {Title}", _currentTitle);

            var formats = new List<VideoFormat>();
            var formatsArray = json["formats"] as JArray;

            if (formatsArray == null) {
                Log.Warning("No formats found");
                return new ResultDto<List<VideoFormat>>(true, "Форматы не найдены", new List<VideoFormat>());
            }

            Log.Debug("Total formats: {Count}", formatsArray.Count);

            foreach (var fmt in formatsArray) {
                cancellationToken.ThrowIfCancellationRequested();

                try {
                    var vcodec = fmt["vcodec"]?.ToString() ?? "";
                    var acodec = fmt["acodec"]?.ToString() ?? "";
                    var formatNote = fmt["format_note"]?.ToString() ?? "";
                    var formatId = fmt["format_id"]?.ToString() ?? "";
                    var height = fmt["height"]?.Type != JTokenType.Null ? fmt["height"]?.Value<int?>() : null;
                    var fps = fmt["fps"]?.Type != JTokenType.Null ? fmt["fps"]?.Value<double?>() : null;
                    var tbr = fmt["tbr"]?.Type != JTokenType.Null ? fmt["tbr"]?.Value<int?>() : null;
                    var filesize = fmt["filesize"]?.Type != JTokenType.Null ? fmt["filesize"]?.Value<long?>() : null;
                    var resolution = fmt["resolution"]?.ToString() ?? "";
                    var ext = fmt["ext"]?.ToString() ?? "";

                    if (formatNote == "storyboard")
                        continue;

                    if (vcodec == "none" && acodec == "none")
                        continue;

                    var format = new VideoFormat {
                        FormatId = formatId,
                        Extension = ext,
                        Resolution = resolution == "audio only" ? (height?.ToString() ?? "") : resolution,
                        FormatNote = formatNote,
                        Filesize = filesize,
                        Tbr = tbr,
                        Vcodec = vcodec,
                        Acodec = acodec,
                        Height = height,
                        Fps = fps,
                        IsAudioOnly = vcodec == "none"
                    };

                    if (!string.IsNullOrEmpty(format.FormatId))
                        formats.Add(format);
                } catch (Exception ex) {
                    Log.Error(ex, "Parse error");
                }
            }

            var videoFormats = formats.GetVideoOnlyFormats();
            var audioFormats = formats.GetAudioFormats();

            var combinedFormats = new List<VideoFormat>();
            foreach (var video in videoFormats) {
                var bestAudio = audioFormats.FirstOrDefault();
                if (bestAudio != null) {
                    combinedFormats.Add(new VideoFormat {
                        FormatId = $"{video.FormatId}+{bestAudio.FormatId}",
                        Extension = "mp4",
                        Resolution = video.Resolution,
                        Height = video.Height,
                        FormatNote = $"{video.FormatNote}+{bestAudio.FormatNote}",
                        IsAudioOnly = false,
                        Vcodec = video.Vcodec,
                        Acodec = bestAudio.Acodec,
                        Tbr = (video.Tbr ?? 0) + (bestAudio.Tbr ?? 0),
                        Filesize = (video.Filesize ?? 0) + (bestAudio.Filesize ?? 0)
                    });
                }
            }

            var fullFormats = formats
                .Where(f => !f.IsAudioOnly && f.Acodec != "none")
                .ToList();

            var resultFormats = new List<VideoFormat>();
            resultFormats.AddRange(combinedFormats);
            resultFormats.AddRange(fullFormats);
            resultFormats.AddRange(audioFormats);

            Log.Information("Result formats: combined={Combined}, full={Full}, audio={Audio}",
                combinedFormats.Count, fullFormats.Count, audioFormats.Count);

            var finalFormats = resultFormats.DistinctBy(i => i.DisplayName).OrderBy(i => i.DisplayName).ToList();

            progress?.Report($"Готово: {_currentTitle}");
            return new ResultDto<List<VideoFormat>>(true, "Успешно", finalFormats);
        } catch (OperationCanceledException) {
            Log.Information("GetFormatsAsync cancelled");
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
        try {
            var outputTemplate = _tempManager.GetOutputTemplate(fileName);

            var result = await DownloadMainAsync(url, formatId, outputTemplate, progress, status);

            if (!result) {
                Log.Warning("DownloadMainAsync failed, trying DownloadSimpleAsync");
                result = await DownloadSimpleAsync(url);

                if (!result) {
                    return new ResultDto<bool>(false, "Ошибка при скачивании", false);
                }
            }

            var downloadedFile = _tempManager.GetFirstFile();
            Log.Debug($"downloadedFile = {downloadedFile}");

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
                    Log.Information("STDOUT: {Line}", line);
                    var percent = _parser.ParsePercent(line);
                    if (percent.HasValue && (int)percent.Value != lastPercent) {
                        lastPercent = (int)percent.Value;
                        progress?.Report(percent.Value);
                        Log.Information("Progress: {Percent}%", percent.Value);
                    }
                },
                onStdErr: line => {
                    Log.Information("STDERR: {Line}", line);

                    var percent = _parser.ParsePercent(line);
                    if (percent.HasValue && (int)percent.Value != lastPercent) {
                        lastPercent = (int)percent.Value;
                        progress?.Report(percent.Value);
                        Log.Information("Progress: {Percent}%", percent.Value);
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