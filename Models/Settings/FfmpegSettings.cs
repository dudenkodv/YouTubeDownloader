using System;
using System.Collections.Generic;
using System.Text;

namespace YouTubeDownloader.Models.Settings;

public class FfmpegSettings {
    public int DefaultMp3Bitrate { get; set; } = 192;
    public string OutputFormat { get; set; } = "mp4";
}
