using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using YouTubeDownloader.Models.Interfaces;
using YouTubeDownloader.Models.Services.Strategies;

namespace YouTubeDownloader.Models.Services;

public class DownloadStrategyFactory : IDownloadStrategyFactory {
    private readonly FormatDownloadStrategy _formatStrategy;
    private readonly SimpleDownloadStrategy _simpleStrategy;

    public DownloadStrategyFactory(FormatDownloadStrategy formatStrategy, SimpleDownloadStrategy simpleStrategy) {
        _formatStrategy = formatStrategy;
        _simpleStrategy = simpleStrategy;
    }

    public IDownloadStrategy CreateFormatStrategy() {
        return _formatStrategy;
    }

    public IDownloadStrategy CreateSimpleStrategy() {
        return _simpleStrategy;
    }
}
