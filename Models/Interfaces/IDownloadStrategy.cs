using YouTubeDownloader.Models.Interfaces;

namespace YouTubeDownloader.Models.Interfaces;

/// <summary>
/// Стратегия скачивания видео
/// </summary>
public interface IDownloadStrategy {
    /// <summary>
    /// Выполняет скачивание видео
    /// </summary>
    /// <param name="executable">Путь к yt-dlp.exe</param>
    /// <param name="url">URL видео</param>
    /// <param name="formatId">ID формата (может быть пустым)</param>
    /// <param name="outputTemplate">Шаблон пути для сохранения</param>
    /// <param name="progress">Прогресс скачивания</param>
    /// <param name="status">Статус операции</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Путь к скачанному файлу или null</returns>
    Task<string?> ExecuteAsync(string executable, string url, string formatId, string outputTemplate,
        IProgress<double>? progress, IProgress<string>? status, CancellationToken cancellationToken);
}