using CliWrap;
using CliWrap.Buffered;
using Microsoft.Extensions.Logging;
using Serilog;
using YouTubeDownloader.Models.Interfaces;

namespace YouTubeDownloader.Models.Services;

public class ProcessExecutor : IProcessExecutor {
    private readonly ILogger<ProcessExecutor> _logger;
    private CancellationTokenSource? _currentCts;

    public ProcessExecutor(ILogger<ProcessExecutor> logger) {
        _logger = logger;
    }

    public async Task<BufferedCommandResult> ExecuteAsync(string executable, string args, CancellationToken cancellationToken = default) {
        try {
            _currentCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            var result = await Cli.Wrap(executable)
                .WithArguments(args)
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync(_currentCts.Token);

            return result;
        } catch (OperationCanceledException) {
            _logger.LogInformation("ProcessExecutor.ExecuteAsync: Отмена получена");
            throw;
        } catch (Exception) {

            throw;
        }
    }

    public async Task ExecuteStreamingAsync(string executable, string args,
        Action<string>? onStdOut = null,
        Action<string>? onStdErr = null,
        CancellationToken cancellationToken = default) {
        _currentCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var cmd = Cli.Wrap(executable)
            .WithArguments(args)
            //.WithValidation(CommandResultValidation.None)
            .WithStandardOutputPipe(PipeTarget.ToDelegate(line => onStdOut?.Invoke(line)))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(line => onStdErr?.Invoke(line)));

        await cmd.ExecuteAsync(_currentCts.Token);
    }

    public void Cancel() {
        _logger.LogInformation("ProcessExecutor.Cancel() вызван. _currentCts is null: {IsNull}", _currentCts is null);
        _logger.LogInformation("Cancel requested");
        _currentCts?.Cancel();
    }
}