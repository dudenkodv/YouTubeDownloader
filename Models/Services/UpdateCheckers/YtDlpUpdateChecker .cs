using Microsoft.Extensions.Logging;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using YouTubeDownloader.Models.Interfaces;

namespace YouTubeDownloader.Models.Services.UpdateCheckers;

public class YtDlpUpdateChecker : BaseUpdateChecker {
    public override string ToolName => "yt-dlp";
    public override string ExecutableName => "yt-dlp.exe";

    public YtDlpUpdateChecker(
        IProcessExecutor processExecutor,
        HttpClient httpClient,
        ILogger<YtDlpUpdateChecker> logger)
        : base(processExecutor, httpClient, logger) {
    }

    public override async Task<string?> GetCurrentVersionAsync(string exePath) {
        if (!File.Exists(exePath))
            return null;

        try {
            var result = await _processExecutor.ExecuteAsync(exePath, "--version");
            LogCurrentVersion(result.StandardOutput);
            return result.ExitCode == 0 ? result.StandardOutput.Trim() : null;
        } catch (Exception ex) {
            _logger.LogError(ex, "Failed to get {Tool} version", ToolName);
            return null;
        }
    }

    public override async Task<string?> GetLatestVersionAsync() {
        try {
            var response = await _httpClient.GetAsync("https://api.github.com/repos/yt-dlp/yt-dlp/releases/latest");
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("tag_name").GetString()?.TrimStart('v');
        } catch (Exception ex) {
            _logger.LogError(ex, "Failed to get latest {Tool} version", ToolName);
            return null;
        }
    }

    public override string GetDownloadUrl(string version) {
        return $"https://github.com/yt-dlp/yt-dlp/releases/download/v{version}/yt-dlp.exe";
    }
}