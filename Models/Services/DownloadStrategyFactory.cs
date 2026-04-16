using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using YouTubeDownloader.Models.Interfaces;
using YouTubeDownloader.Models.Services.Strategies;

namespace YouTubeDownloader.Models.Services;

public class DownloadStrategyFactory : IDownloadStrategyFactory {
    private readonly IDownloadStrategy _formatStrategy;
    private readonly IDownloadStrategy _simpleStrategy;

    public DownloadStrategyFactory(IDownloadStrategy formatStrategy, IDownloadStrategy simpleStrategy) {
        _formatStrategy = formatStrategy;
        _simpleStrategy = simpleStrategy;
    }

    public IDownloadStrategy Create(bool useFormat = true) {
        return useFormat ? _formatStrategy : _simpleStrategy;
    }
}
