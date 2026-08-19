using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using YouTubeDownloader.Models.DTOs;
using YouTubeDownloader.Models.Interfaces;

namespace YouTubeDownloader.Models.Services.UpdateCheckers;

public class DenoUpdateChecker : IUpdateChecker {
    private readonly IProcessExecutor _processExecutor;
    private readonly HttpClient _httpClient;
    private readonly ILogger<DenoUpdateChecker> _logger;

    public string ToolName => "Deno";
    public string ExecutableName => "deno.exe";

    public DenoUpdateChecker(IProcessExecutor processExecutor, HttpClient httpClient, ILogger<DenoUpdateChecker> logger) {
        _processExecutor = processExecutor;
        _httpClient = httpClient;
        _logger = logger;

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "YouTubeDownloader/1.0");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
    }

    public async Task<string?> GetCurrentVersionAsync(string exePath) {
        if (!File.Exists(exePath))
            return null;

        try {
            var result = await _processExecutor.ExecuteAsync(exePath, "--version");
            if (result.ExitCode != 0)
                return null;

            var output = result.StandardOutput.Trim();
            var match = Regex.Match(output, @"deno\s+(\S+)");
            return match.Success ? match.Groups[1].Value : output;
        } catch (Exception ex) {
            _logger.LogError(ex, "Failed to get {Tool} version", ToolName);
            return null;
        }
    }

    public async Task<string?> GetLatestVersionAsync() {
        try {
            var response = await _httpClient.GetAsync("https://api.github.com/repos/denoland/deno/releases/latest");
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("tag_name").GetString()?.TrimStart('v');
        } catch (HttpRequestException ex) {
            _logger.LogWarning(ex, "Network error while checking Deno version");
            return null;
        } catch (Exception ex) {
            _logger.LogError(ex, "Failed to get latest {Tool} version", ToolName);
            return null;
        }
    }

    public string GetDownloadUrl(string version) {
        return $"https://github.com/denoland/deno/releases/download/v{version}/deno-x86_64-pc-windows-msvc.zip";
    }

    public async Task<ResultDto<bool>> DownloadAndInstallAsync(string downloadUrl, string exePath, IProgress<int>? progress) {
        try {
            var tempFile = Path.GetTempFileName() + ".zip";

            using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var total = response.Content.Headers.ContentLength ?? -1;
            var downloaded = 0L;

            await using var stream = await response.Content.ReadAsStreamAsync();
            await using var file = new FileStream(tempFile, FileMode.Create);

            var buffer = new byte[8192];
            int read;
            while ((read = await stream.ReadAsync(buffer)) > 0) {
                await file.WriteAsync(buffer.AsMemory(0, read));
                downloaded += read;
                progress?.Report(total > 0 ? (int)(downloaded * 100 / total) : 0);
            }

            progress?.Report(100);

            var extractDir = Path.Combine(Path.GetTempPath(), "DenoExtract_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(extractDir);

            System.IO.Compression.ZipFile.ExtractToDirectory(tempFile, extractDir);
            var extractedExe = Directory.GetFiles(extractDir, "deno.exe", SearchOption.AllDirectories).FirstOrDefault();

            if (extractedExe == null) {
                Directory.Delete(extractDir, true);
                return new ResultDto<bool>(false, "Deno.exe not found in archive", false);
            }

            var backupPath = exePath + ".backup";
            if (File.Exists(backupPath))
                File.Delete(backupPath);
            if (File.Exists(exePath))
                File.Move(exePath, backupPath);

            File.Move(extractedExe, exePath);

            // Очистка
            Directory.Delete(extractDir, true);
            File.Delete(tempFile);
            if (File.Exists(backupPath))
                File.Delete(backupPath);

            _logger.LogInformation("Deno updated to {Version}", Path.GetFileName(exePath));
            return new ResultDto<bool>(true, "Update installed successfully", true);
        } catch (Exception ex) {
            _logger.LogError(ex, "Failed to download and install {Tool}", ToolName);
            return new ResultDto<bool>(false, ex.Message, false);
        }
    }
}