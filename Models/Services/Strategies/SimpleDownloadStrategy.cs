using Microsoft.Extensions.Logging;
using YouTubeDownloader.Models.Entities;
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

    protected override string BuildArgs(string url, string formatId, string outputTemplate, DownloadTypeEnum downloadType) {
        return CommandBuilder.Create()
            .Verbose()
            .ImpersonateChrome()
            .AllowU()
            .Format(downloadType == DownloadTypeEnum.Audio ? "bestaudio" : "bestvideo+bestaudio")
            .Output(outputTemplate)
            .NoWarnings()
            .Newline()
            .Progress()
            .Url(url)
            .Build();
    }
    protected override string GetStatusMessage() => "(простой режим)...";
}