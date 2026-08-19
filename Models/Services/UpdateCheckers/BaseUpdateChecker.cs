using Microsoft.Extensions.Logging;
using System.IO;
using System.Net.Http;
using YouTubeDownloader.Models.DTOs;
using YouTubeDownloader.Models.Interfaces;

namespace YouTubeDownloader.Models.Services.UpdateCheckers;

public abstract class BaseUpdateChecker : IUpdateChecker {
    protected readonly IProcessExecutor _processExecutor;
    protected readonly HttpClient _httpClient;
    protected readonly ILogger _logger;

    protected BaseUpdateChecker(
        IProcessExecutor processExecutor,
        HttpClient httpClient,
        ILogger logger) {
        _processExecutor = processExecutor;
        _httpClient = httpClient;
        _logger = logger;
    }

    public abstract string ToolName { get; }
    public abstract string ExecutableName { get; }
    public abstract Task<string?> GetCurrentVersionAsync(string exePath);
    public abstract Task<string?> GetLatestVersionAsync();
    public abstract string GetDownloadUrl(string version);

    public virtual async Task<ResultDto<bool>> DownloadAndInstallAsync(string downloadUrl, string exePath, IProgress<int>? progress) {
        try {
            var tempFile = await DownloadFileAsync(downloadUrl, progress);
            var extractedFile = await ExtractFileAsync(tempFile);

            if (string.IsNullOrEmpty(extractedFile))
                return new ResultDto<bool>(false, $"{ToolName} executable not found", false);

            ReplaceFile(extractedFile, exePath);
            Cleanup(tempFile);

            return new ResultDto<bool>(true, "Update installed successfully", true);
        } catch (Exception ex) {
            _logger.LogError(ex, "Failed to download and install {Tool}", ToolName);
            return new ResultDto<bool>(false, ex.Message, false);
        }
    }

    protected virtual async Task<string> DownloadFileAsync(string downloadUrl, IProgress<int>? progress) {
        var tempFile = Path.GetTempFileName();

        using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? -1;
        var downloaded = 0L;

        await using var stream = await response.Content.ReadAsStreamAsync();
        await using var file = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None);

        var buffer = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(buffer)) > 0) {
            await file.WriteAsync(buffer.AsMemory(0, read));
            downloaded += read;
            progress?.Report(total > 0 ? (int)(downloaded * 100 / total) : 0);
        }

        progress?.Report(100);
        return tempFile;
    }

    protected virtual Task<string?> ExtractFileAsync(string tempFile) {
        return Task.FromResult<string?>(tempFile);
    }

    protected virtual void ReplaceFile(string sourceFile, string destinationPath) {
        var backupPath = destinationPath + ".backup";
        if (File.Exists(backupPath))
            File.Delete(backupPath);
        if (File.Exists(destinationPath))
            File.Move(destinationPath, backupPath);
        File.Move(sourceFile, destinationPath);
        if (File.Exists(backupPath))
            File.Delete(backupPath);
    }

    protected virtual void Cleanup(string tempFile) {
        if (File.Exists(tempFile))
            File.Delete(tempFile);
    }

    protected void LogCurrentVersion(string version) {
        _logger.LogInformation($"{ToolName} current version: {version}");
    }
}