using Microsoft.Extensions.Logging;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using YouTubeDownloader.Models.Interfaces;

namespace YouTubeDownloader.Models.Services.UpdateCheckers;

public class DenoUpdateChecker : BaseUpdateChecker {
    public override string ToolName => "Deno";
    public override string ExecutableName => "deno.exe";

    public DenoUpdateChecker(
        IProcessManager processManager,
        IProcessExecutor processExecutor,
        HttpClient httpClient,
        ILogger<DenoUpdateChecker> logger)
        : base(processManager, processExecutor, httpClient, logger) {
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "YouTubeDownloader/1.0");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
    }

    public override async Task<string?> GetCurrentVersionAsync(string exePath) {
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

    public override async Task<string?> GetLatestVersionAsync() {
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

    public override string GetDownloadUrl(string version) {
        return $"https://github.com/denoland/deno/releases/download/v{version}/deno-x86_64-pc-windows-msvc.zip";
    }

    protected override async Task<string?> ExtractFileAsync(string tempFile) {
        var extractDir = Path.Combine(Path.GetTempPath(), $"{ToolName}Extract_{Guid.NewGuid()}");
        Directory.CreateDirectory(extractDir);

        await Task.Run(() => ZipFile.ExtractToDirectory(tempFile, extractDir));

        var extractedExe = Directory.GetFiles(extractDir, ExecutableName, SearchOption.AllDirectories).FirstOrDefault();

        if (extractedExe == null) {
            try { Directory.Delete(extractDir, true); } catch (Exception ex) { _logger.LogWarning(ex, "Failed to delete extract dir"); }
            return null;
        }

        return extractedExe;
    }

    protected override void Cleanup(string tempFile) {
        base.Cleanup(tempFile);

        var extractDir = Path.GetDirectoryName(tempFile)?.Replace(".zip", "");
        if (!string.IsNullOrEmpty(extractDir) && Directory.Exists(extractDir)) {
            try { Directory.Delete(extractDir, true); } catch (Exception ex) { _logger.LogWarning(ex, "Failed to cleanup extract dir"); }
        }
    }
}