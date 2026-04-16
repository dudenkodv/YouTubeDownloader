using System;
using System.Collections.Generic;
using System.Text;

namespace YouTubeDownloader.Models.Interfaces;

public interface ITempFileManager : IDisposable {
    string TempDirectory { get; }
    string GetOutputTemplate(string fileName);
    string? GetFirstFile();
}
