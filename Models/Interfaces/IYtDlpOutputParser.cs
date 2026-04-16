namespace YouTubeDownloader.Models.Interfaces;

/// <summary>
/// Парсер вывода yt-dlp
/// </summary>
public interface IYtDlpOutputParser {
    /// <summary>
    /// Извлекает процент прогресса из строки вывода
    /// </summary>
    double? ParsePercent(string line);

    /// <summary>
    /// Извлекает путь к файлу из строки вывода
    /// </summary>
    string? ParseDestination(string line);
}