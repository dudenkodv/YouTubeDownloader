using Microsoft.Extensions.Logging;
using YouTubeDownloader.Models.Interfaces;
using YouTubeDownloader.Models.Services.Arguments;

namespace YouTubeDownloader.Models.Services.Strategies;

/// <summary>
/// Простая стратегия скачивания (без указания формата)
/// </summary>
public class SimpleDownloadStrategy : BaseDownloadStrategy {
    public SimpleDownloadStrategy(
        IProcessExecutor executor,
        IYtDlpOutputParser parser,
        ILogger<SimpleDownloadStrategy> logger) : base(executor, parser, logger) {
    }

    protected override string BuildArgs(string url, string formatId, string outputTemplate) {
        return CommandBuilder.Create()
            .Verbose()
            .Output(outputTemplate)
            .Url(url)
            .Build();
    }

    protected override string GetStatusMessage() => "Скачивание видео (простой режим)...";
}