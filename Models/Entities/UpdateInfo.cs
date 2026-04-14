namespace YouTubeDownloader.Models.Entities;

public class UpdateInfo {
    public string ToolName { get; set; } = string.Empty;
    public string CurrentVersion { get; set; } = string.Empty;
    public string LatestVersion { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public bool IsUpdateAvailable { get; set; }
    public long? NewFileSize { get; set; }
}