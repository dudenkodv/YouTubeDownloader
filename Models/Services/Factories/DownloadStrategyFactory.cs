using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using YouTubeDownloader.Models.Entities;
using YouTubeDownloader.Models.Interfaces;
using YouTubeDownloader.Models.Interfaces.Factories;
using YouTubeDownloader.Models.Services.Strategies;

namespace YouTubeDownloader.Models.Services.Factories;

public class DownloadStrategyFactory : IDownloadStrategyFactory {
    private readonly FormatDownloadStrategy _formatStrategy;
    private readonly SimpleDownloadStrategy _simpleStrategy;

    public DownloadStrategyFactory(FormatDownloadStrategy formatStrategy, SimpleDownloadStrategy simpleStrategy) {
        _formatStrategy = formatStrategy;
        _simpleStrategy = simpleStrategy;
    }

    public IDownloadStrategy Create(DownloadStrategyTypeEnum type) {
        return type switch {
            DownloadStrategyTypeEnum.Format => _formatStrategy,
            DownloadStrategyTypeEnum.Simple => _simpleStrategy,
            _ => throw new NotSupportedException($"Strategy {type} not supported")
        };
    }
}
