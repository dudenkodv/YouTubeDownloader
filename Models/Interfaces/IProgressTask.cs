using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace YouTubeDownloader.Models.Interfaces;

/// <summary>
/// Интерфейс задачи с прогрессом
/// </summary>
public interface IProgressTask {
    string Id { get; set; }
    string Name { get; set; }
    string Status { get; set; }
    double Progress { get; set; }
    bool IsActive { get; set; }
    CancellationToken CancellationToken { get; }
    CancellationTokenSource CreateCancellationTokenSource();
    void Cancel();
    void Dispose();
    Task ExecuteAsync(IProgress<double> progress, IProgress<string> status, CancellationToken cancellationToken);
    event PropertyChangedEventHandler? PropertyChanged;
}
