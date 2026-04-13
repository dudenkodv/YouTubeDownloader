using YouTubeDownloader.Models;

namespace YouTubeDownloader.Interfaces;

/// <summary>
/// Сервис для работы с YouTube
/// </summary>
public interface IYouTubeService {
    /// <summary>
    /// Получает список доступных форматов для видео
    /// </summary>
    /// <param name="url">Ссылка на YouTube видео</param>
    /// <param name="progress">Прогресс выполнения операции</param>
    /// <returns>Список форматов видео/аудио</returns>
    Task<List<VideoFormat>> GetFormatsAsync(string url, IProgress<string>? progress = null);

    /// <summary>
    /// Получает название видео с YouTube
    /// </summary>
    /// <returns>Название видео или null</returns>
    Task<string?> GetVideoTitleAsync();

    /// <summary>
    /// Скачивает видео или аудио с YouTube
    /// </summary>
    /// <param name="url">Ссылка на YouTube видео</param>
    /// <param name="formatId">ID формата для скачивания</param>
    /// <param name="outputPath">Путь для сохранения файла</param>
    /// <param name="fileName">Желаемое имя файла (без расширения)</param>
    /// <param name="progress">Прогресс загрузки (0-100)</param>
    /// <param name="status">Текстовый статус операции</param>
    /// <param name="cancellationToken">Токен для отмены операции</param>
    Task DownloadAsync(string url, string formatId, string outputPath, string fileName,
        IProgress<double>? progress = null,
        IProgress<string>? status = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Отменяет текущую загрузку
    /// </summary>
    void CancelDownload();
}