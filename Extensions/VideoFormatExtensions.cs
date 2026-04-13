using YouTubeDownloader.Models;

namespace YouTubeDownloader.Extensions; 
public static class VideoFormatExtensions {
    public static List<VideoFormat> GetVideoOnlyFormats(this List<VideoFormat> formats) {
        return formats
            .Where(f => !f.IsAudioOnly && f.Height.HasValue && f.Height > 0 && f.Acodec == "none")
            .GroupBy(f => f.Height.Value)
            .Select(g => g.OrderByDescending(f => f.Height).First())
            .OrderBy(f => f.Height)
            .ToList();
    }

    public static List<VideoFormat> GetAudioFormats(this List<VideoFormat> formats) {
        return formats
            .Where(f => f.IsAudioOnly)
            .OrderByDescending(f => f.Tbr ?? 0)
            .ToList();
    }
}