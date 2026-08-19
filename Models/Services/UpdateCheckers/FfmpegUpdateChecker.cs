using Microsoft.Extensions.Logging;
using YouTubeDownloader.Models.DTOs;
using YouTubeDownloader.Models.Interfaces;

namespace YouTubeDownloader.Models.Services.UpdateCheckers;

public class FfmpegUpdateChecker : IUpdateChecker {
    public string ToolName => "FFmpeg";
    public string ExecutableName => "ffmpeg.exe";

    public Task<string?> GetCurrentVersionAsync(string exePath) {
        return Task.FromResult<string?>("не поддерживается");
    }

    public Task<string?> GetLatestVersionAsync() {
        return Task.FromResult<string?>("не поддерживается");
    }

    public string GetDownloadUrl(string version) {
        return string.Empty;
    }

    public Task<ResultDto<bool>> DownloadAndInstallAsync(string downloadUrl, string exePath, IProgress<int>? progress) {
        return Task.FromResult(new ResultDto<bool>(false, "Автообновление FFmpeg не поддерживается", false));
    }
}