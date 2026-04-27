using System.Globalization;
using System.Xml.Xsl;
using YouTubeDownloader.Models.Interfaces;

namespace YouTubeDownloader.Models.Services;

/// <summary>
/// Парсер вывода yt-dlp
/// </summary>
public class YtDlpOutputParser : IYtDlpOutputParser {

    private static readonly System.Text.RegularExpressions.Regex DestinationRegex =
        new System.Text.RegularExpressions.Regex(@"Destination:\s*(.+)$");

    public double? ParsePercent(string line) {
        if (string.IsNullOrEmpty(line))
            return null;

        if (!line.Contains("[download]") || !line.Contains('%'))
            return null;
        //регулярка не парсит строку вида
        //[download]   0.0% of  169.63MiB at  281.99KiB/s ETA 10:15
        var strPercent = line.Split(" ").FirstOrDefault(str => str.Contains('%'))?.TrimEnd('%');
        if(double.TryParse(strPercent, NumberStyles.Any, CultureInfo.InvariantCulture, out var percent))
            return percent;

        return null;
    }

    public string? ParseDestination(string line) {
        if (string.IsNullOrEmpty(line))
            return null;

        if (!line.Contains("[download]") || !line.Contains("Destination:"))
            return null;

        var match = DestinationRegex.Match(line);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }
}