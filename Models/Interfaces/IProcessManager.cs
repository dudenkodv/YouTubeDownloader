namespace YouTubeDownloader.Models.Interfaces;

public interface IProcessManager {
    Task KillProcessesAsync(string processName, CancellationToken cancellationToken = default);
    Task<bool> WaitForProcessExitAsync(string processName, TimeSpan timeout, CancellationToken cancellationToken = default);
}