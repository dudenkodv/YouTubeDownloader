using Microsoft.Extensions.Logging;
using YouTubeDownloader.Models.Interfaces;
using YouTubeDownloader.Models.Services.Arguments;

namespace YouTubeDownloader.Models.Services.Strategies;

/// <summary>
/// Стратегия скачивания с указанным форматом
/// </summary>
public class FormatDownloadStrategy : IDownloadStrategy {
    private readonly IProcessExecutor _executor;
    private readonly IYtDlpOutputParser _parser;
    private readonly ILogger<FormatDownloadStrategy> _logger;

    public FormatDownloadStrategy(
        IProcessExecutor executor,
        IYtDlpOutputParser parser, 
        ILogger<FormatDownloadStrategy> logger) {
        _executor = executor;
        _parser = parser;
        _logger = logger;
    }

    public async Task<string?> ExecuteAsync(string executable, string url, string formatId, string outputTemplate,
        IProgress<double>? progress, IProgress<string>? status, CancellationToken cancellationToken) {
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
            _logger.LogInformation("FormatDownloadStrategy отменён");
            throw;
        } catch (Exception) {

            throw;
        }
    }
}