using Microsoft.Extensions.Logging;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using YouTubeDownloader.Models.DTOs;
using YouTubeDownloader.Models.Interfaces;

namespace YouTubeDownloader.Models.Services.UpdateCheckers;

public class YtDlpUpdateChecker : BaseUpdateChecker {
    // Старый (стабильный)
    // private const string ApiUrl = "https://api.github.com/repos/yt-dlp/yt-dlp/releases/latest";
    // private const string DownloadBaseUrl = "https://github.com/yt-dlp/yt-dlp/releases/download";

    // Новый (nightly)
    private const string ApiUrl = "https://api.github.com/repos/yt-dlp/yt-dlp-nightly-builds/releases/latest";
    private const string DownloadBaseUrl = "https://github.com/yt-dlp/yt-dlp-nightly-builds/releases/download";

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
            var response = await _httpClient.GetAsync(ApiUrl);
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
        // В nightly-репозитории файл называется yt-dlp.exe
        // но иногда может быть с префиксом. Пробуем оба варианта.
        // Основной вариант:
        return $"{DownloadBaseUrl}/v{version}/yt-dlp.exe";
    }

    // Переопределяем метод скачивания, чтобы получить реальный URL из API
    public override async Task<ResultDto<bool>> DownloadAndInstallAsync(string downloadUrl, string exePath, IProgress<int>? progress) {
        try {
            // Получаем реальный URL из API
            var response = await _httpClient.GetAsync(ApiUrl);
            if (!response.IsSuccessStatusCode)
                return new ResultDto<bool>(false, "Failed to get release info", false);

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            // Ищем assets с yt-dlp.exe
            var assets = doc.RootElement.GetProperty("assets");
            string? assetUrl = null;

            foreach (var asset in assets.EnumerateArray()) {
                var name = asset.GetProperty("name").GetString();
                if (name == "yt-dlp.exe") {
                    assetUrl = asset.GetProperty("browser_download_url").GetString();
                    break;
                }
            }

            if (string.IsNullOrEmpty(assetUrl)) {
                _logger.LogError("yt-dlp.exe not found in nightly release assets");
                return new ResultDto<bool>(false, "yt-dlp.exe not found in release", false);
            }

            _logger.LogInformation("Downloading from: {Url}", assetUrl);

            var tempFile = await DownloadFileAsync(assetUrl, progress);
            var extractedFile = await ExtractFileAsync(tempFile);

            if (string.IsNullOrEmpty(extractedFile))
                return new ResultDto<bool>(false, "Failed to extract file", false);

            ReplaceFile(extractedFile, exePath);
            Cleanup(tempFile);

            return new ResultDto<bool>(true, "Update installed successfully", true);
        } catch (Exception ex) {
            _logger.LogError(ex, "Failed to download and install {Tool}", ToolName);
            return new ResultDto<bool>(false, ex.Message, false);
        }
    }
}