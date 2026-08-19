using Microsoft.Extensions.Logging;
using Serilog;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using YouTubeDownloader.Models.DTOs;
using YouTubeDownloader.Models.Entities;
using YouTubeDownloader.Models.Interfaces;

namespace YouTubeDownloader.Models.Services;

public class UpdateService : IUpdateService {
    private readonly IProcessManager _processManager;
    private readonly HttpClient _httpClient;
    private readonly ILogger<UpdateService> _logger;
    private readonly string _toolsDir;

    public UpdateService(ILogger<UpdateService> logger, IProcessManager processManager) {
        _processManager = processManager;
        _toolsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools");
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "YouTubeDownloader/1.0");
        _logger = logger;

        if (!Directory.Exists(_toolsDir))
            Directory.CreateDirectory(_toolsDir);
    }

    public void Dispose() {
        _httpClient.Dispose();
    }

    public async Task<ResultDto<UpdateInfo>> CheckYtDlpUpdateAsync() {
        try {
            var ytDlpPath = Path.Combine(_toolsDir, "yt-dlp.exe");
            var currentVersion = await GetLocalYtDlpVersionAsync(ytDlpPath);
            var latestRelease = await GetLatestYtDlpReleaseAsync();

            if (latestRelease == null) {
                return new ResultDto<UpdateInfo>(false, "Не удалось получить информацию о последней версии", null!);
            }

            var updateInfo = new UpdateInfo {
                ToolName = "yt-dlp",
                CurrentVersion = currentVersion ?? "не установлен",
                LatestVersion = latestRelease.TagName?.TrimStart('v') ?? "неизвестно",
                IsUpdateAvailable = false
            };

            if (!string.IsNullOrEmpty(currentVersion)) {
                var latestVersion = latestRelease.TagName.TrimStart('v');
                updateInfo.LatestVersion = latestVersion;
                updateInfo.IsUpdateAvailable = currentVersion != latestVersion;

                var asset = latestRelease.Assets.FirstOrDefault(a => a.Name == "yt-dlp.exe");
                if (asset != null) {
                    updateInfo.DownloadUrl = asset.BrowserDownloadUrl;
                    updateInfo.NewFileSize = asset.Size;
                }
            }

            return new ResultDto<UpdateInfo>(true, "Успешно", updateInfo);
        } catch (Exception ex) {
            _logger.LogError(ex, "CheckYtDlpUpdateAsync error");
            return new ResultDto<UpdateInfo>(false, ex.Message, null!);
        }
    }

    public async Task<ResultDto<UpdateInfo>> CheckFfmpegUpdateAsync() {
        try {
            var ffmpegPath = Path.Combine(_toolsDir, "ffmpeg.exe");
            var currentVersion = await GetLocalFfmpegVersionAsync(ffmpegPath);

            var updateInfo = new UpdateInfo {
                ToolName = "FFmpeg",
                CurrentVersion = currentVersion ?? "не установлен",
                LatestVersion = "актуальная",
                DownloadUrl = string.Empty,
                IsUpdateAvailable = false
            };

            return new ResultDto<UpdateInfo>(true, "Успешно", updateInfo);
        } catch (Exception ex) {
            _logger.LogError(ex, "CheckFfmpegUpdateAsync error");
            return new ResultDto<UpdateInfo>(false, ex.Message, null!);
        }
    }

    public async Task<ResultDto<bool>> DownloadAndUpdateToolAsync(UpdateInfo updateInfo, IProgress<int>? progress = null) {
        if (string.IsNullOrEmpty(updateInfo.DownloadUrl))
            return new ResultDto<bool>(false, "Download URL not found", false);

        var tempFile = Path.GetTempFileName();
        var toolPath = Path.Combine(_toolsDir, "yt-dlp.exe");
        var backupPath = toolPath + ".backup";

        try {
            await _processManager.KillProcessesAsync("yt-dlp");
            await _processManager.WaitForProcessExitAsync("yt-dlp", TimeSpan.FromSeconds(2));

            await DownloadFileAsync(updateInfo.DownloadUrl, tempFile, progress);
            ReplaceToolFile(tempFile, toolPath, backupPath);

            return new ResultDto<bool>(true, "Update installed successfully", true);
        } catch (Exception ex) {
            _logger.LogError(ex, "Update failed");
            RestoreBackup(toolPath, backupPath);
            return new ResultDto<bool>(false, ex.Message, false);
        } finally {
            CleanupFiles(tempFile, backupPath);
        }
    }

    private async Task<string?> GetLocalYtDlpVersionAsync(string exePath) {
        if (!File.Exists(exePath))
            return null;

        try {
            var processInfo = new ProcessStartInfo {
                FileName = exePath,
                Arguments = "--version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process == null)
                return null;

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            return output.Trim();
        } catch (Exception ex) {
            _logger.LogError(ex, "GetLocalYtDlpVersionAsync error");
            return null;
        }
    }

    private async Task<string?> GetLocalFfmpegVersionAsync(string exePath) {
        if (!File.Exists(exePath))
            return null;

        try {
            var processInfo = new ProcessStartInfo {
                FileName = exePath,
                Arguments = "-version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process == null)
                return null;

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            var match = Regex.Match(output, @"ffmpeg version (\S+)");
            return match.Success ? match.Groups[1].Value : output.Split('\n').FirstOrDefault()?.Trim();
        } catch (Exception ex) {
            _logger.LogError(ex, "GetLocalFfmpegVersionAsync error");
            return null;
        }
    }

    private async Task<YtDlpReleaseDto?> GetLatestYtDlpReleaseAsync() {
        try {
            var response = await _httpClient.GetAsync("https://api.github.com/repos/yt-dlp/yt-dlp/releases/latest");
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<YtDlpReleaseDto>(json);
        } catch (Exception ex) {
            _logger.LogError(ex, "GetLatestYtDlpReleaseAsync error");
            return null;
        }
    }

    private async Task DownloadFileAsync(string url, string destination, IProgress<int>? progress) {
        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? -1;
        var downloaded = 0L;

        await using var stream = await response.Content.ReadAsStreamAsync();
        await using var file = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);

        var buffer = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(buffer)) > 0) {
            await file.WriteAsync(buffer.AsMemory(0, read));
            downloaded += read;
            progress?.Report(total > 0 ? (int)(downloaded * 100 / total) : 0);
        }
    }

    private void ReplaceToolFile(string tempFile, string toolPath, string backupPath) {
        if (File.Exists(backupPath))
            File.Delete(backupPath);

        if (File.Exists(toolPath))
            File.Move(toolPath, backupPath);

        File.Move(tempFile, toolPath);
    }

    private void RestoreBackup(string toolPath, string backupPath) {
        if (File.Exists(backupPath) && !File.Exists(toolPath))
            File.Move(backupPath, toolPath);
    }

    private void CleanupFiles(params string[] paths) {
        foreach (var path in paths.Where(File.Exists)) {
            try { File.Delete(path); } catch (Exception ex) { _logger.LogWarning(ex, "Failed to delete {Path}", path); }
        }
    }
}