using System;
using System.Collections.Generic;
using System.Text;

namespace YouTubeDownloader.Models.Interfaces;

public interface IFfmpegConverter {
    Task ConvertToMp3Async(string inputPath, string outputPath, int bitrate = 192);
}
