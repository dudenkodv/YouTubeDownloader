using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using System.IO;
using YouTubeDownloader.Models.Interfaces;

namespace YouTubeDownloader.Models.Services;

public class FfmpegConverter : IFfmpegConverter {
    private readonly string _ffmpegPath;
    private readonly IProcessExecutor _executor;
    private readonly ILogger<FfmpegConverter> _logger;

    public FfmpegConverter(IProcessExecutor executor, ILogger<FfmpegConverter> logger) {
        _executor = executor;
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        _ffmpegPath = Path.Combine(baseDir, "Tools", "ffmpeg.exe");
    }

    public async Task ConvertToMp3Async(string inputPath, string outputPath, int bitrate = 192) {
        var args = $"-i \"{inputPath}\" -c:a libmp3lame -b:a {bitrate}k \"{outputPath}\" -y";
        var result = await _executor.ExecuteAsync(_ffmpegPath, args);

        if (result.ExitCode != 0) {
            Log.Error("FFmpeg error: {Error}", result.StandardError);
            throw new Exception($"Ошибка FFmpeg: {result.StandardError}");
        }

        _logger.LogInformation("Conversion complete: {Output}", outputPath);
    }
}