using System.Text.Json.Serialization;

namespace YouTubeDownloader.DTOs;

/// <summary>
/// DTO для ответа GitHub API о релизе yt-dlp
/// </summary>
public class YtDlpReleaseDto {
    [JsonPropertyName("tag_name")]
    public string TagName { get; set; } = string.Empty;

    [JsonPropertyName("assets")]
    public List<YtDlpAssetDto> Assets { get; set; } = new();
}

/// <summary>
/// DTO для ассета (файла) в релизе yt-dlp
/// </summary>
public class YtDlpAssetDto {
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }
}