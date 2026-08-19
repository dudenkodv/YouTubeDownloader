using Microsoft.Extensions.Logging;
using System.IO;
using YouTubeDownloader.Models.DTOs;
using YouTubeDownloader.Models.Entities;
using YouTubeDownloader.Models.Interfaces;

namespace YouTubeDownloader.Models.Services;

public class UpdateService : IUpdateService {
    private readonly IProcessManager _processManager;
    private readonly ILogger<UpdateService> _logger;
    private readonly string _toolsDir;
    private readonly Dictionary<string, IUpdateChecker> _checkers;

    public UpdateService(
        IProcessManager processManager,
        ILogger<UpdateService> logger,
        IEnumerable<IUpdateChecker> checkers) {
        _processManager = processManager;
        _logger = logger;
        _toolsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools");
        _checkers = checkers.ToDictionary(c => c.ToolName);
    }

    public async Task<ResultDto<UpdateInfo>> CheckYtDlpUpdateAsync() {
        return await CheckUpdateAsync("yt-dlp");
    }

    public async Task<ResultDto<UpdateInfo>> CheckDenoUpdateAsync() {
        return await CheckUpdateAsync("Deno");
    }

    public async Task<ResultDto<UpdateInfo>> CheckFfmpegUpdateAsync() {
        return await CheckUpdateAsync("FFmpeg");
    }

    private async Task<ResultDto<UpdateInfo>> CheckUpdateAsync(string toolName) {
        if (!_checkers.TryGetValue(toolName, out var checker))
            return new ResultDto<UpdateInfo>(false, $"Checker for {toolName} not found", null!);

        try {
            var exePath = Path.Combine(_toolsDir, checker.ExecutableName);
            var currentVersion = await checker.GetCurrentVersionAsync(exePath);
            var latestVersion = await checker.GetLatestVersionAsync();

            var updateInfo = new UpdateInfo {
                ToolName = toolName,
                CurrentVersion = currentVersion ?? "не установлен",
                LatestVersion = latestVersion ?? "неизвестно",
                IsUpdateAvailable = !string.IsNullOrEmpty(currentVersion) &&
                                   !string.IsNullOrEmpty(latestVersion) &&
                                   currentVersion != latestVersion,
                DownloadUrl = latestVersion != null ? checker.GetDownloadUrl(latestVersion) : string.Empty
            };

            return new ResultDto<UpdateInfo>(true, "Успешно", updateInfo);
        } catch (Exception ex) {
            _logger.LogError(ex, "CheckUpdateAsync error for {Tool}", toolName);
            return new ResultDto<UpdateInfo>(false, ex.Message, null!);
        }
    }

    public async Task<ResultDto<bool>> DownloadAndUpdateToolAsync(UpdateInfo updateInfo, IProgress<int>? progress = null) {
        if (!_checkers.TryGetValue(updateInfo.ToolName, out var checker))
            return new ResultDto<bool>(false, $"Checker for {updateInfo.ToolName} not found", false);

        try {
            var exePath = Path.Combine(_toolsDir, checker.ExecutableName);
            await _processManager.KillProcessesAsync(Path.GetFileNameWithoutExtension(checker.ExecutableName));
            await _processManager.WaitForProcessExitAsync(Path.GetFileNameWithoutExtension(checker.ExecutableName), TimeSpan.FromSeconds(2));

            return await checker.DownloadAndInstallAsync(updateInfo.DownloadUrl, exePath, progress);
        } catch (Exception ex) {
            _logger.LogError(ex, "DownloadAndUpdateToolAsync error for {Tool}", updateInfo.ToolName);
            return new ResultDto<bool>(false, ex.Message, false);
        }
    }

    public void Dispose() {
    }
}