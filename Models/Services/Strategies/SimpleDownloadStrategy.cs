using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using YouTubeDownloader.Models.Interfaces;
using YouTubeDownloader.Models.Services.Arguments;

namespace YouTubeDownloader.Models.Services.Strategies;

/// <summary>
/// Простая стратегия скачивания (без указания формата)
/// </summary>
public class SimpleDownloadStrategy : IDownloadStrategy {
    private readonly IProcessExecutor _executor;
    private readonly IYtDlpOutputParser _parser;
    private readonly ILogger<SimpleDownloadStrategy> _logger;

    public SimpleDownloadStrategy(
        IProcessExecutor executor,
        IYtDlpOutputParser parser) {
        _executor = executor;
        _parser = parser;
        //_logger = logger;
    }

    public async Task<string?> ExecuteAsync(string executable, string url, string formatId, string outputTemplate,
        IProgress<double>? progress, IProgress<string>? status, CancellationToken cancellationToken) {
        var args = CommandBuilder.Create()
            .Verbose()
            .Url(url)
            .Build();

        status?.Report("Скачивание видео (простой режим)...");

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
    }
}
