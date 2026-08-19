using YouTubeDownloader.Models.DTOs;
using YouTubeDownloader.Models.Entities;

namespace YouTubeDownloader.Models.Interfaces;

public interface IUpdateChecker {
    string ToolName { get; }
    string ExecutableName { get; }

    Task<string?> GetCurrentVersionAsync(string exePath);
    Task<string?> GetLatestVersionAsync();
    string GetDownloadUrl(string version);
    Task<ResultDto<bool>> DownloadAndInstallAsync(string downloadUrl, string exePath, IProgress<int>? progress);
}