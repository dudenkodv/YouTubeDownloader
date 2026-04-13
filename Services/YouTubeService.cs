using CliWrap;
using CliWrap.Buffered;
using Newtonsoft.Json.Linq;
using Serilog;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using YouTubeDownloader.Interfaces;
using YouTubeDownloader.Models;

namespace YouTubeDownloader.Services;

public class YouTubeService : IYouTubeService {
    private readonly string ytDlpPath;
    private readonly string ffmpegPath;
    private CancellationTokenSource? downloadCts;
    private string currentTitle = string.Empty;

    public YouTubeService() {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        ytDlpPath = Path.Combine(baseDir, "Tools", "yt-dlp.exe");
        ffmpegPath = Path.Combine(baseDir, "Tools", "ffmpeg.exe");
    }

    public async Task<List<VideoFormat>> GetFormatsAsync(string url, IProgress<string>? progress = null, CancellationToken cancellationToken = default) {
        Log.Information("GetFormatsAsync: {Url}", url);
        progress?.Report("Получение информации о видео...");

        cancellationToken.ThrowIfCancellationRequested();
        Log.Debug("Cancel check passed at start of GetFormatsAsync");

        var args = $"--impersonate chrome --force-ipv4 --geo-bypass --extractor-args \"youtube:player_client=web,android,ios\" --verbose -J --no-warnings \"{url}\"";
        Log.Debug("Arguments: {Args}", args);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var result = await Cli.Wrap(ytDlpPath)
            .WithArguments(args)
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync(cts.Token);

        Log.Information("ExitCode: {ExitCode}", result.ExitCode);

        if (!string.IsNullOrEmpty(result.StandardError))
            Log.Debug("STDERR: {Error}", result.StandardError);

        if (result.ExitCode != 0) {
            Log.Error("Error: {Error}", result.StandardError);
            throw new Exception($"Ошибка yt-dlp: {result.StandardError}");
        }

        cancellationToken.ThrowIfCancellationRequested();
        Log.Debug("Cancel check after executing yt-dlp");

        var json = JObject.Parse(result.StandardOutput);
        currentTitle = json["title"]?.ToString() ?? "Unknown";
        Log.Information("Title: {Title}", currentTitle);

        var formats = new List<VideoFormat>();
        var formatsArray = json["formats"] as JArray;

        if (formatsArray == null) {
            Log.Warning("No formats found");
            return formats;
        }

        Log.Debug("Total formats: {Count}", formatsArray.Count);

        int index = 0;
        foreach (var fmt in formatsArray) {
            cancellationToken.ThrowIfCancellationRequested();
            Log.Debug("Cancel check inside formats loop, iteration {Index}", index++);

            try {
                var vcodec = fmt["vcodec"]?.ToString() ?? "";
                var acodec = fmt["acodec"]?.ToString() ?? "";
                var formatNote = fmt["format_note"]?.ToString() ?? "";

                if (formatNote == "storyboard")
                    continue;

                var format = new VideoFormat {
                    FormatId = fmt["format_id"]?.ToString() ?? "",
                    Extension = fmt["ext"]?.ToString() ?? "",
                    Resolution = fmt["resolution"]?.ToString() ?? "",
                    FormatNote = formatNote,
                    Filesize = fmt["filesize"]?.Type != JTokenType.Null ? fmt["filesize"]?.Value<long?>() : null,
                    Tbr = fmt["tbr"]?.Type != JTokenType.Null ? fmt["tbr"]?.Value<int?>() : null,
                    Vcodec = vcodec,
                    Acodec = acodec,
                    Height = fmt["height"]?.Type != JTokenType.Null ? fmt["height"]?.Value<int?>() : null,
                    Fps = fmt["fps"]?.Type != JTokenType.Null ? fmt["fps"]?.Value<double?>() : null,
                    IsAudioOnly = vcodec == "none"
                };

                if (!string.IsNullOrEmpty(format.FormatId))
                    formats.Add(format);
            } catch (Exception ex) {
                Log.Error(ex, "Parse error");
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        Log.Debug("Cancel check before grouping formats");

        var videoFormats = formats
            .Where(f => !f.IsAudioOnly && f.Height.HasValue && f.Height > 0)
            .GroupBy(f => f.Height.Value)
            .Select(g => g.OrderByDescending(f => f.Height).First())
            .OrderBy(f => f.Height)
            .ToList();

        var audioFormats = formats
            .Where(f => f.IsAudioOnly)
            .OrderByDescending(f => f.Tbr ?? 0)
            .Take(3)
            .ToList();

        var resultFormats = new List<VideoFormat>();
        resultFormats.AddRange(videoFormats);
        resultFormats.AddRange(audioFormats);

        Log.Information("Result formats: video={Video}, audio={Audio}", videoFormats.Count, audioFormats.Count);

        progress?.Report($"Готово: {currentTitle}");
        return resultFormats;
    }

    public Task<string?> GetVideoTitleAsync() {
        return Task.FromResult<string?>(currentTitle);
    }

    public async Task DownloadAsync(string url, string formatId, string outputPath, string fileName,
        IProgress<double>? progress = null,
        IProgress<string>? status = null,
        CancellationToken cancellationToken = default) {
        downloadCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var tempDir = Path.Combine(Path.GetTempPath(), "YTDownloader_" + Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try {
            var outputTemplate = Path.Combine(tempDir, $"{fileName}.%(ext)s");

            string? downloadedFile = null;

            var result = await DownloadMainAsync(url, formatId, outputTemplate, progress, status);
            if (result)
                Log.Information("DownloadMainAsync success");
            else
                Log.Information("DownloadMainAsync error");

            if (!result) {
                Log.Error("ошибка при скачивании DownloadMainAsync");
                result = await DownloadSimpleAsync(url, tempDir);
                if (result)
                    Log.Information("DownloadSimpleAsync success");
                else
                    Log.Information("DownloadSimpleAsync error");
            } else if (!result) {
                Log.Error("ошибка при скачивании DownloadSimpleAsync");
                throw new Exception("Файл не найден после загрузки");
            }

            downloadedFile = Directory.GetFiles(tempDir).FirstOrDefault();
            Log.Debug($"downloadedFile = {downloadedFile}");

            if (string.IsNullOrEmpty(downloadedFile) || !File.Exists(downloadedFile))
                throw new Exception("Файл не найден после загрузки");

            await ProcessDownloadedFileAsync(downloadedFile, outputPath, fileName, progress, status);
        } catch (OperationCanceledException) {
            Log.Information("Download cancelled");
            throw;
        } catch (Exception ex) {
            Log.Error(ex.Message);
        } finally {
            if (Directory.Exists(tempDir)) {
                try { Directory.Delete(tempDir, true); } catch (Exception ex) { Log.Warning(ex, "Failed to delete temp dir"); }
            }
            downloadCts?.Dispose();
            downloadCts = null;
        }
    }

    private async Task<bool> DownloadSimpleAsync(string url, string outputPath) {
        try {
            var args = $"--verbose \"{url}\"";
            await Cli.Wrap(ytDlpPath)
                .WithArguments(args)
                .ExecuteAsync(downloadCts!.Token);
            return true;
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception ex) {
            Log.Error(ex.Message);
            return false;
        }
    }

    private async Task<bool> DownloadMainAsync(string url, string formatId, string outputTemplate, IProgress<double>? progress = null,
        IProgress<string>? status = null) {
        try {
            var args = $"-f {formatId} -o \"{outputTemplate}\" --verbose --no-warnings --newline --progress \"{url}\"";
            await ExecuteDownloadAsync(args, progress, status);
            return true;
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception ex) {
            Log.Error(ex.Message);
            return false;
        }
    }

    private async Task<string?> ExecuteDownloadAsync(string args,
        IProgress<double>? progress = null, IProgress<string>? status = null) {
        var lastPercent = 0;
        string? downloadedFile = null;

        var cmd = Cli.Wrap(ytDlpPath)
            .WithArguments(args)
            .WithStandardOutputPipe(PipeTarget.ToDelegate(line => {
                Log.Information("STDOUT: {Line}", line);

                var percent = ParsePercent(line);
                if (percent.HasValue && (int)percent.Value != lastPercent) {
                    lastPercent = (int)percent.Value;
                    progress?.Report(percent.Value);
                    Log.Information("Progress: {Percent}%", percent.Value);
                }
            }))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(line => {
                Log.Information("STDERR: {Line}", line);

                var percent = ParsePercent(line);
                if (percent.HasValue && (int)percent.Value != lastPercent) {
                    lastPercent = (int)percent.Value;
                    progress?.Report(percent.Value);
                    Log.Information("Progress: {Percent}%", percent.Value);
                }

                if (line.Contains("[download]") && line.Contains("Destination:")) {
                    var parts = line.Split(':', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                        downloadedFile = parts[1].Trim();
                }
            }));

        await cmd.ExecuteAsync(downloadCts!.Token);
        return downloadedFile;
    }

    private double? ParsePercent(string line) {
        if (!line.Contains("[download]") || !line.Contains('%'))
            return null;

        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts) {
            if (part.Contains('%')) {
                var cleaned = part.TrimEnd('%').Replace(',', '.');
                if (double.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
                    return result;
            }
        }
        return null;
    }

    private async Task ProcessDownloadedFileAsync(string downloadedFile, string outputPath, string fileName,
        IProgress<double>? progress = null, IProgress<string>? status = null) {
        status?.Report("Обработка...");

        var extension = Path.GetExtension(downloadedFile).ToLower();
        string finalPath;

        if (extension == ".m4a") {
            finalPath = Path.Combine(outputPath, fileName + ".mp3");
            await ConvertToMp3Async(downloadedFile, finalPath);
            File.Delete(downloadedFile);
        } else {
            finalPath = Path.Combine(outputPath, fileName + extension);
            if (File.Exists(finalPath))
                File.Delete(finalPath);
            File.Move(downloadedFile, finalPath);
        }

        status?.Report($"Сохранено: {Path.GetFileName(finalPath)}");
        progress?.Report(100);
    }

    public void CancelDownload() {
        Log.Information("CancelDownload called");
        downloadCts?.Cancel();
    }

    private async Task ConvertToMp3Async(string inputPath, string outputPath) {
        var args = $"-i \"{inputPath}\" -c:a libmp3lame -b:a 192k \"{outputPath}\" -y";
        var result = await Cli.Wrap(ffmpegPath)
            .WithArguments(args)
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync();

        if (result.ExitCode != 0) {
            Log.Error("FFmpeg error: {Error}", result.StandardError);
            throw new Exception($"Ошибка FFmpeg: {result.StandardError}");
        }

        Log.Information("Conversion complete: {Output}", outputPath);
    }
}