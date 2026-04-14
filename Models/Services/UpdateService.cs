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
    private readonly string _toolsDir;
    private readonly HttpClient _httpClient;

    public UpdateService() {
        _toolsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools");
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "YouTubeDownloader/1.0");

        if (!Directory.Exists(_toolsDir))
            Directory.CreateDirectory(_toolsDir);
    }

    public void Dispose() {
        _httpClient.Dispose();
    }

    public async Task<UpdateInfo> CheckYtDlpUpdateAsync() {
        var ytDlpPath = Path.Combine(_toolsDir, "yt-dlp.exe");
        var currentVersion = await GetLocalYtDlpVersionAsync(ytDlpPath);
        var latestRelease = await GetLatestYtDlpReleaseAsync();

        var updateInfo = new UpdateInfo {
            ToolName = "yt-dlp",
            CurrentVersion = currentVersion ?? "не установлен",
            LatestVersion = latestRelease?.TagName?.TrimStart('v') ?? "неизвестно",
            IsUpdateAvailable = false
        };

        if (latestRelease != null && !string.IsNullOrEmpty(currentVersion)) {
            var latestVersion = latestRelease.TagName.TrimStart('v');
            updateInfo.LatestVersion = latestVersion;
            updateInfo.IsUpdateAvailable = currentVersion != latestVersion;

            // Находим ссылку на yt-dlp.exe
            var asset = latestRelease.Assets.FirstOrDefault(a => a.Name == "yt-dlp.exe");
            if (asset != null) {
                updateInfo.DownloadUrl = asset.BrowserDownloadUrl;
                updateInfo.NewFileSize = asset.Size;
            }
        }

        return updateInfo;
    }

    public async Task<UpdateInfo> CheckFfmpegUpdateAsync() {
        var ffmpegPath = Path.Combine(_toolsDir, "ffmpeg.exe");
        var currentVersion = await GetLocalFfmpegVersionAsync(ffmpegPath);

        return new UpdateInfo {
            ToolName = "FFmpeg",
            CurrentVersion = currentVersion ?? "не установлен",
            LatestVersion = "актуальная",
            DownloadUrl = string.Empty,
            IsUpdateAvailable = false
        };
    }

    public async Task<bool> DownloadAndUpdateToolAsync(UpdateInfo updateInfo, IProgress<int>? progress = null) {
        if (string.IsNullOrEmpty(updateInfo.DownloadUrl))
            return false;

        var tempFile = Path.GetTempFileName();
        var toolPath = Path.Combine(_toolsDir, "yt-dlp.exe");
        var backupPath = toolPath + ".backup";

        try {
            progress?.Report(0);

            using var response = await _httpClient.GetAsync(updateInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1;
            var downloadedBytes = 0L;

            await using var contentStream = await response.Content.ReadAsStreamAsync();
            await using var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            int bytesRead;
            while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0) {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                downloadedBytes += bytesRead;

                if (totalBytes > 0)
                    progress?.Report((int)((double)downloadedBytes / totalBytes * 100));
            }

            progress?.Report(100);

            if (File.Exists(backupPath))
                File.Delete(backupPath);

            if (File.Exists(toolPath))
                File.Move(toolPath, backupPath);

            File.Move(tempFile, toolPath);

            return true;
        } catch {
            if (File.Exists(backupPath) && !File.Exists(toolPath))
                File.Move(backupPath, toolPath);

            throw;
        } finally {
            if (File.Exists(tempFile))
                File.Delete(tempFile);

            if (File.Exists(backupPath))
                File.Delete(backupPath);
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
        } catch {
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
        } catch {
            return null;
        }
    }

    private async Task<YtDlpReleaseDto?> GetLatestYtDlpReleaseAsync() {
        try {
            var response = await _httpClient.GetAsync("https://api.github.com/repos/yt-dlp/yt-dlp/releases/latest");
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            var release = JsonSerializer.Deserialize<YtDlpReleaseDto>(json);

            return release;
        } catch {
            return null;
        }
    }
}