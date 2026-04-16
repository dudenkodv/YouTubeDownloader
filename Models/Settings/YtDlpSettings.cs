using System;
using System.Collections.Generic;
using System.Text;

namespace YouTubeDownloader.Models.Settings;

public class YtDlpSettings {
    public string YtDlpPath { get; set; } = "Tools/yt-dlp.exe";
    public string FfmpegPath { get; set; } = "Tools/ffmpeg.exe";
    public int DownloadTimeoutSeconds { get; set; } = 300;
    public int MaxRetries { get; set; } = 3;
}
