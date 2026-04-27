using Microsoft.Extensions.Logging;
using System.IO;
using YouTubeDownloader.Models.Interfaces;

namespace YouTubeDownloader.Models.Services;

public class TempFileManager : ITempFileManager {
    private readonly string _tempDirectory;
    private bool _disposed;
    private readonly ILogger<TempFileManager> _logger;

    public TempFileManager(ILogger<TempFileManager> logger) {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "YTDownloader_" + Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDirectory);
        _logger = logger;
        _logger.LogInformation($"_tempDirectory: {_tempDirectory}");
    }

    public string TempDirectory => _tempDirectory;

    public string GetOutputTemplate(string fileName) {
        return Path.Combine(_tempDirectory, $"{fileName}.%(ext)s");
    }

    public string? GetFirstFile() {
        return Directory.GetFiles(_tempDirectory).FirstOrDefault();
    }

    public void Cleanup() {
        if (Directory.Exists(_tempDirectory)) {
            try {
                Directory.Delete(_tempDirectory, true);
            } catch (Exception ex) {
                _logger.LogError(ex, "Failed to delete temp dir");
            }
        }
    }

    public void Dispose() {
        if (!_disposed) {
            Cleanup();
            _disposed = true;
        }
    }
}