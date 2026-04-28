using Microsoft.Extensions.Logging;
using YouTubeDownloader.Models.Entities;
using YouTubeDownloader.Models.Extensions;
using YouTubeDownloader.Models.Interfaces;
using YouTubeDownloader.Models.Services.Arguments;

namespace YouTubeDownloader.Models.Services.Strategies;

/// <summary>
/// Базовый класс для стратегий скачивания
/// </summary>
public abstract class BaseDownloadStrategy : IDownloadStrategy {
    protected readonly IProcessExecutor _executor;
    protected readonly IYtDlpOutputParser _parser;
    protected readonly ILogger _logger;

    protected BaseDownloadStrategy(
        IProcessExecutor executor,
        IYtDlpOutputParser parser,
        ILogger logger) {
        _executor = executor;
        _parser = parser;
        _logger = logger;
    }

    /// <summary>
    /// Формирует аргументы командной строки для yt-dlp
    /// </summary>
    protected abstract string BuildArgs(string url, string formatId, string outputTemplate);

    public async Task<string?> ExecuteAsync(string executable, string url, string formatId, string outputTemplate,
    IProgress<double>? progress, IProgress<string>? status,
    CancellationToken cancellationToken, DownloadType downloadType) {
        try {
            var args = BuildArgs(url, formatId, outputTemplate);

            status?.Report(downloadType.GetDescription());

            string? downloadedFile = null;
            var lastPercent = 0;

            await _executor.ExecuteStreamingAsync(executable, args,
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
                },
                cancellationToken: cancellationToken);

            return downloadedFile;
        } catch (OperationCanceledException) {
            _logger.LogInformation("{StrategyName} отменён", GetType().Name);
            throw;
        } catch (Exception ex) {
            _logger.LogError(ex, "{StrategyName} ошибка", GetType().Name);
            throw;
        }
    }
}