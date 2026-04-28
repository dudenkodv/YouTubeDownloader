using YouTubeDownloader.Models.Entities;
using YouTubeDownloader.Models.DTOs;

namespace YouTubeDownloader.Models.Interfaces;

/// <summary>
/// Сервис для работы с YouTube
/// </summary>
public interface IYouTubeService {
    /// <summary>
    /// Получает список доступных форматов для видео
    /// </summary>
    /// <param name="url">Ссылка на YouTube видео</param>
    /// <param name="progress">Прогресс выполнения операции</param>
    /// <param name="cancellationToken">Токен для отмены операции</param>
    /// <returns>Результат операции со списком форматов</returns>
    Task<ResultDto<List<VideoFormat>>> GetFormatsAsync(string url, IProgress<string>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Получает название видео с YouTube
    /// </summary>
    /// <returns>Название видео или null</returns>
    string GetVideoTitle();

    /// <summary>
    /// Скачивает видео или аудио с YouTube
    /// </summary>
    /// <param name="url">Ссылка на YouTube видео</param>
    /// <param name="formatId">ID формата для скачивания</param>
    /// <param name="outputPath">Путь для сохранения файла</param>
    /// <param name="fileName">Желаемое имя файла (без расширения)</param>
    /// <param name="isVideoMode">Режима скачивания видео или аудио</param>
    /// <param name="progress">Прогресс загрузки (0-100)</param>
    /// <param name="status">Текстовый статус операции</param>
    /// <param name="cancellationToken">Токен для отмены операции</param>
    /// <returns>Результат операции</returns>
    Task<ResultDto<bool>> DownloadAsync(string url, string formatId, string outputPath, string fileName, bool isVideoMode,
        IProgress<double>? progress = null,
        IProgress<string>? status = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Отменяет текущую загрузку
    /// </summary>
    void CancelDownload();
}