using CliWrap;
using CliWrap.Buffered;
using Newtonsoft.Json.Linq;
using Serilog;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using YouTubeDownloader.Interfaces;
using YouTubeDownloader.Models;

namespace YouTubeDownloader.Services;

public class YouTubeService : IYouTubeService {
    private readonly string _ytDlpPath;
    private readonly string _ffmpegPath;
    private CancellationTokenSource? _downloadCts;
    private string _currentTitle = string.Empty;

    public YouTubeService() {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        _ytDlpPath = Path.Combine(baseDir, "Tools", "yt-dlp.exe");
        _ffmpegPath = Path.Combine(baseDir, "Tools", "ffmpeg.exe");
    }

    public async Task<List<VideoFormat>> GetFormatsAsync(string url, IProgress<string>? progress = null) {
        Log.Information("GetFormatsAsync: {Url}", url);
        progress?.Report("Получение информации о видео...");

        // РАБОТАЮЩИЕ аргументы
        var args = $"--impersonate chrome --force-ipv4 --geo-bypass --extractor-args \"youtube:player_client=web,android,ios\" --verbose -J --no-warnings \"{url}\"";
        Log.Debug("Arguments: {Args}", args);

        var result = await Cli.Wrap(_ytDlpPath)
            .WithArguments(args)
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync();

        Log.Information("ExitCode: {ExitCode}", result.ExitCode);

        if (!string.IsNullOrEmpty(result.StandardError))
            Log.Debug("STDERR: {Error}", result.StandardError);

        if (result.ExitCode != 0) {
            Log.Error("Error: {Error}", result.StandardError);
            throw new Exception($"Ошибка yt-dlp: {result.StandardError}");
        }

        var json = JObject.Parse(result.StandardOutput);
        _currentTitle = json["title"]?.ToString() ?? "Unknown";
        Log.Information("Title: {Title}", _currentTitle);

        var formats = new List<VideoFormat>();
        var formatsArray = json["formats"] as JArray;

        if (formatsArray == null) {
            Log.Warning("No formats found");
            return formats;
        }

        Log.Debug("Total formats: {Count}", formatsArray.Count);

        foreach (var fmt in formatsArray) {
            try {
                var vcodec = fmt["vcodec"]?.ToString() ?? "";
                var acodec = fmt["acodec"]?.ToString() ?? "";
                var formatNote = fmt["format_note"]?.ToString() ?? "";

                // Skip storyboards
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

        // Group video formats by height
        var videoFormats = formats
            .Where(f => !f.IsAudioOnly && f.Height.HasValue && f.Height > 0)
            .GroupBy(f => f.Height.Value)
            .Select(g => g.OrderByDescending(f => f.Height).First())
            .OrderBy(f => f.Height)
            .ToList();

        // Take best audio formats
        var audioFormats = formats
            .Where(f => f.IsAudioOnly)
            .OrderByDescending(f => f.Tbr ?? 0)
            .Take(3)
            .ToList();

        var resultFormats = new List<VideoFormat>();
        resultFormats.AddRange(videoFormats);
        resultFormats.AddRange(audioFormats);

        Log.Information("Result formats: video={Video}, audio={Audio}", videoFormats.Count, audioFormats.Count);

        progress?.Report($"Готово: {_currentTitle}");
        return resultFormats;
    }

    public Task<string?> GetVideoTitleAsync() {
        return Task.FromResult<string?>(_currentTitle);
    }

    public async Task DownloadAsync(string url, string formatId, string outputPath, string fileName,
        IProgress<double>? progress = null,
        IProgress<string>? status = null,
        CancellationToken cancellationToken = default) {
        _downloadCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var tempDir = Path.Combine(Path.GetTempPath(), "YTDownloader_" + Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try {
            var outputTemplate = Path.Combine(tempDir, $"{fileName}.%(ext)s");

            string? downloadedFile = null;

            var result = await DownloadMainAsync(url, formatId, outputTemplate, progress, status);
            if(!result)
                result = await DownloadSimpleAsync(url, tempDir);
            if (!result) {
                Log.Error("ошибка при скачивании");
                return;
            }                

            downloadedFile = Directory.GetFiles(tempDir).FirstOrDefault();

            if (string.IsNullOrEmpty(downloadedFile) || !File.Exists(downloadedFile))
                throw new Exception("Файл не найден после загрузки");

            await ProcessDownloadedFileAsync(downloadedFile, outputPath, fileName, progress, status);
        } catch (Exception ex) {
            Log.Error(ex.Message);
        } finally {
            if (Directory.Exists(tempDir)) {
                try { Directory.Delete(tempDir, true); } catch (Exception ex) { Log.Warning(ex, "Failed to delete temp dir"); }
            }
            _downloadCts?.Dispose();
            _downloadCts = null;
        }
    }
    private async Task<bool> DownloadSimpleAsync(string url, string outputPath) {
        try {
            var args = $"--verbose \"{url}\"";
            await Cli.Wrap(_ytDlpPath)
                .WithArguments(args)
                .ExecuteAsync(_downloadCts!.Token);
            return true;
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
        } catch (Exception ex) {
            Log.Error(ex.Message);
            return false;
        }
    }

    private async Task<string?> ExecuteDownloadAsync(string args,
        IProgress<double>? progress = null, IProgress<string>? status = null) {
        var progressRegex = new Regex(@"(\\d+(?:\\.\\d+)?)%");
        var lastPercent = 0;
        string? downloadedFile = null;

        var cmd = Cli.Wrap(_ytDlpPath)
            .WithArguments(args)
            .WithStandardOutputPipe(PipeTarget.ToDelegate(line => {
                var match = progressRegex.Match(line);
                if (match.Success && double.TryParse(match.Groups[1].Value, out var percent)) {
                    if ((int)percent != lastPercent) {
                        lastPercent = (int)percent;
                        progress?.Report(percent);
                    }
                }
            }))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(line => {
                Log.Debug("stderr: {Line}", line);

                var match = progressRegex.Match(line);
                if (match.Success && double.TryParse(match.Groups[1].Value, out var percent)) {
                    if ((int)percent != lastPercent) {
                        lastPercent = (int)percent;
                        progress?.Report(percent);
                    }
                }

                if (line.Contains("[download]") && line.Contains("Destination:")) {
                    var matchPath = Regex.Match(line, @"Destination:\s*(.+)$");
                    if (matchPath.Success)
                        downloadedFile = matchPath.Groups[1].Value.Trim();
                }
            }));

        await cmd.ExecuteAsync(_downloadCts!.Token);
        return downloadedFile;
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
        _downloadCts?.Cancel();
    }

    private async Task ConvertToMp3Async(string inputPath, string outputPath) {
        var args = $"-i \"{inputPath}\" -c:a libmp3lame -b:a 192k \"{outputPath}\" -y";
        var result = await Cli.Wrap(_ffmpegPath)
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