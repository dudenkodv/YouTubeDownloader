using Microsoft.Extensions.Logging;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using YouTubeDownloader.Models.DTOs;
using YouTubeDownloader.Models.Interfaces;

namespace YouTubeDownloader.Models.Services.UpdateCheckers;

public class YtDlpUpdateChecker : IUpdateChecker {
    private readonly IProcessExecutor _processExecutor;
    private readonly HttpClient _httpClient;
    private readonly ILogger<YtDlpUpdateChecker> _logger;

    public string ToolName => "yt-dlp";
    public string ExecutableName => "yt-dlp.exe";

    public YtDlpUpdateChecker(
        IProcessExecutor processExecutor,
        HttpClient httpClient,
        ILogger<YtDlpUpdateChecker> logger) {
        _processExecutor = processExecutor;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<string?> GetCurrentVersionAsync(string exePath) {
        if (!File.Exists(exePath))
            return null;

        try {
            var result = await _processExecutor.ExecuteAsync(exePath, "--version");
            return result.ExitCode == 0 ? result.StandardOutput.Trim() : null;
        } catch (Exception ex) {
            _logger.LogError(ex, "Failed to get {Tool} version", ToolName);
            return null;
        }
    }

    public async Task<string?> GetLatestVersionAsync() {
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

    public string GetDownloadUrl(string version) {
        return $"https://github.com/yt-dlp/yt-dlp/releases/download/v{version}/yt-dlp.exe";
    }

    public async Task<ResultDto<bool>> DownloadAndInstallAsync(string downloadUrl, string exePath, IProgress<int>? progress) {
        try {
            var tempFile = Path.GetTempFileName();

            using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var total = response.Content.Headers.ContentLength ?? -1;
            var downloaded = 0L;

            await using var stream = await response.Content.ReadAsStreamAsync();
            await using var file = new FileStream(tempFile, FileMode.Create, FileAccess.Write);

            var buffer = new byte[8192];
            int read;
            while ((read = await stream.ReadAsync(buffer)) > 0) {
                await file.WriteAsync(buffer.AsMemory(0, read));
                downloaded += read;
                progress?.Report(total > 0 ? (int)(downloaded * 100 / total) : 0);
            }

            progress?.Report(100);

            var backupPath = exePath + ".backup";
            if (File.Exists(backupPath))
                File.Delete(backupPath);
            if (File.Exists(exePath))
                File.Move(exePath, backupPath);
            File.Move(tempFile, exePath);
            if (File.Exists(backupPath))
                File.Delete(backupPath);

            return new ResultDto<bool>(true, "Update installed successfully", true);
        } catch (Exception ex) {
            _logger.LogError(ex, "Failed to download and install {Tool}", ToolName);
            return new ResultDto<bool>(false, ex.Message, false);
        }
    }
}