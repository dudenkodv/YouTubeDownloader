using YouTubeDownloader.Models;

namespace YouTubeDownloader.Interfaces;

/// <summary>
/// Сервис для проверки и установки обновлений внешних инструментов
/// </summary>
public interface IUpdateService : IDisposable {
    /// <summary>
    /// Проверяет наличие обновлений для yt-dlp
    /// </summary>
    /// <returns>Информация об обновлении</returns>
    Task<UpdateInfo> CheckYtDlpUpdateAsync();

    /// <summary>
    /// Проверяет наличие обновлений для FFmpeg
    /// </summary>
    /// <returns>Информация об обновлении</returns>
    Task<UpdateInfo> CheckFfmpegUpdateAsync();

    /// <summary>
    /// Скачивает и устанавливает обновление для инструмента
    /// </summary>
    /// <param name="updateInfo">Информация об обновлении</param>
    /// <param name="progress">Прогресс скачивания (0-100)</param>
    /// <returns>Успешно ли выполнено обновление</returns>
    Task<bool> DownloadAndUpdateToolAsync(UpdateInfo updateInfo, IProgress<int>? progress = null);
}