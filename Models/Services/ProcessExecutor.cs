using CliWrap;
using CliWrap.Buffered;

namespace YouTubeDownloader.Models.Services;

public class ProcessExecutor {
    private CancellationTokenSource? _currentCts;

    public async Task<BufferedCommandResult> ExecuteAsync(string executable, string args, CancellationToken cancellationToken = default) {
        _currentCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var result = await Cli.Wrap(executable)
            .WithArguments(args)
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync(_currentCts.Token);

        return result;
    }

    public async Task ExecuteStreamingAsync(string executable, string args,
        Action<string>? onStdOut = null,
        Action<string>? onStdErr = null,
        CancellationToken cancellationToken = default) {
        _currentCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var cmd = Cli.Wrap(executable)
            .WithArguments(args)
            .WithStandardOutputPipe(PipeTarget.ToDelegate(line => onStdOut?.Invoke(line)))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(line => onStdErr?.Invoke(line)));

        await cmd.ExecuteAsync(_currentCts.Token);
    }

    public void Cancel() {
        _currentCts?.Cancel();
    }
}