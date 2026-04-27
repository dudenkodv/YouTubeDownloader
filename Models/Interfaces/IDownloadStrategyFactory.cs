using System;
using System.Collections.Generic;
using System.Text;
using YouTubeDownloader.Models.Entities;

namespace YouTubeDownloader.Models.Interfaces;

/// <summary>
/// Фабрика стратегий скачивания
/// </summary>
public interface IDownloadStrategyFactory {
    IDownloadStrategy Create(DownloadStrategyType type);
}
