using System.Diagnostics;
using Microsoft.Extensions.Logging;
using YouTubeDownloader.Models.Interfaces;

namespace YouTubeDownloader.Models.Services;

public class ProcessManager : IProcessManager {
    private readonly ILogger<ProcessManager> _logger;

    public ProcessManager(ILogger<ProcessManager> logger) {
        _logger = logger;
    }

    public async Task KillProcessesAsync(string processName, CancellationToken cancellationToken = default) {
        var processes = Process.GetProcessesByName(processName);
        if (processes.Length == 0)
            return;

        _logger.LogInformation("Terminating {Count} processes: {Name}", processes.Length, processName);

        foreach (var process in processes) {
            try {
                process.Kill();
                await process.WaitForExitAsync(cancellationToken);
                _logger.LogDebug("Process {Id} terminated", process.Id);
            } catch (Exception ex) {
                _logger.LogWarning(ex, "Failed to terminate process {Id}", process.Id);
            } finally {
                process.Dispose();
            }
        }
    }

    public async Task<bool> WaitForProcessExitAsync(string processName, TimeSpan timeout, CancellationToken cancellationToken = default) {
        var start = DateTime.UtcNow;
        while (DateTime.UtcNow - start < timeout) {
            cancellationToken.ThrowIfCancellationRequested();
            var processes = Process.GetProcessesByName(processName);
            if (processes.Length == 0)
                return true;
            await Task.Delay(100, cancellationToken);
        }
        return false;
    }
}