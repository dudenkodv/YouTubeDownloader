using CliWrap.Buffered;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;
using YouTubeDownloader.Models.Interfaces;

namespace YouTubeDownloader.Models.Services;

public class ProcessKillExecutor : IProcessExecutor {
    private readonly ILogger<ProcessKillExecutor> _logger;
    private Process? _currentProcess;
    private readonly object _lock = new();

    public ProcessKillExecutor(ILogger<ProcessKillExecutor> logger) {
        _logger = logger;
    }

    public async Task<BufferedCommandResult> ExecuteAsync(string executable, string args, CancellationToken cancellationToken = default) {
        var tcs = new TaskCompletionSource<BufferedCommandResult>();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var process = new Process {
            StartInfo = new ProcessStartInfo {
                FileName = executable,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        lock (_lock) {
            _currentProcess = process;
        }

        var output = new StringBuilder();
        var error = new StringBuilder();

        process.OutputDataReceived += (s, e) => { if (e.Data != null) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (s, e) => { if (e.Data != null) error.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var registration = cancellationToken.Register(() => {
            try {
                lock (_lock) {
                    if (_currentProcess != null && !_currentProcess.HasExited) {
                        _currentProcess.Kill();
                        _logger.LogInformation("Process killed by cancellation");
                    }
                }
            } catch (Exception ex) {
                _logger.LogError(ex, "Error killing process");
            }
        });

        await process.WaitForExitAsync();

        lock (_lock) {
            if (_currentProcess == process)
                _currentProcess = null;
        }

        var result = new BufferedCommandResult(process.ExitCode, process.StartTime, process.ExitTime, output.ToString(), error.ToString());

        return result;
    }

    public async Task ExecuteStreamingAsync(string executable, string args,
        Action<string>? onStdOut = null,
        Action<string>? onStdErr = null,
        CancellationToken cancellationToken = default) {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var process = new Process {
            StartInfo = new ProcessStartInfo {
                FileName = executable,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        lock (_lock) {
            _currentProcess = process;
        }

        process.Start();

        using var registration = cancellationToken.Register(() => {
            try {
                lock (_lock) {
                    if (_currentProcess != null && !_currentProcess.HasExited) {
                        _currentProcess.Kill();
                        _logger.LogInformation("Process killed by cancellation");
                    }
                }
            } catch (Exception ex) {
                _logger.LogError(ex, "Error killing process");
            }
        });

        var outputTask = Task.Run(() => {
            while (!process.StandardOutput.EndOfStream) {
                var line = process.StandardOutput.ReadLine();
                if (line != null)
                    onStdOut?.Invoke(line);
            }
        });

        var errorTask = Task.Run(() => {
            while (!process.StandardError.EndOfStream) {
                var line = process.StandardError.ReadLine();
                if (line != null)
                    onStdErr?.Invoke(line);
            }
        });

        await Task.WhenAll(outputTask, errorTask);
        await process.WaitForExitAsync();

        lock (_lock) {
            if (_currentProcess == process)
                _currentProcess = null;
        }
    }

    public void Cancel() {
        _logger.LogInformation("ProcessKillExecutor.Cancel() called");
        lock (_lock) {
            if (_currentProcess != null && !_currentProcess.HasExited) {
                _logger.LogInformation("Killing process: {ProcessId}", _currentProcess.Id);
                _currentProcess.Kill();
            }
        }
    }
}