using CliWrap.Buffered;
using System;
using System.Collections.Generic;
using System.Text;

namespace YouTubeDownloader.Models.Interfaces;

public interface IProcessExecutor {
    Task<BufferedCommandResult> ExecuteAsync(string executable, string args, CancellationToken ct = default);
    Task ExecuteStreamingAsync(string executable, string args, Action<string>? onStdOut = null, Action<string>? onStdErr = null, CancellationToken cancellationToken = default);
    void Cancel();
}
