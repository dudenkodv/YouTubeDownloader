using System;
using System.Collections.Generic;
using System.Text;

namespace YouTubeDownloader.Models.Interfaces;

/// <summary>
/// Фабрика стратегий скачивания
/// </summary>
public interface IDownloadStrategyFactory {
    IDownloadStrategy CreateFormatStrategy();
    IDownloadStrategy CreateSimpleStrategy();
}
