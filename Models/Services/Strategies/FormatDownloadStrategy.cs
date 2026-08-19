using Microsoft.Extensions.Logging;
using YouTubeDownloader.Models.Entities;
using YouTubeDownloader.Models.Interfaces;
using YouTubeDownloader.Models.Services.Arguments;

namespace YouTubeDownloader.Models.Services.Strategies;

/// <summary>
/// Стратегия скачивания с указанным форматом
/// </summary>
public class FormatDownloadStrategy : BaseDownloadStrategy {
    public FormatDownloadStrategy(
        IProcessExecutor executor,
        IYtDlpOutputParser parser,
        ILogger<FormatDownloadStrategy> logger) : base(executor, parser, logger) {
    }

    protected override string BuildArgs(string url, string formatId, string outputTemplate, DownloadTypeEnum downloadType) {
        return CommandBuilder.Create()
            .Verbose()
            .ImpersonateChrome()
            //.ExtractorArgs("youtube:player_client=android")
            .Format(formatId)
            .Output(outputTemplate)
            .NoWarnings()
            .Newline()
            .Progress()
            .Url(url)
            .Build();
    }
    protected override string GetStatusMessage() => "(основной режим)...";
}