using YouTubeDownloader.Models.Interfaces;

namespace YouTubeDownloader.Models.Services;

/// <summary>
/// Парсер вывода yt-dlp
/// </summary>
public class YtDlpOutputParser : IYtDlpOutputParser {
    private static readonly System.Text.RegularExpressions.Regex ProgressRegex =
        new System.Text.RegularExpressions.Regex(@"(\\d+(?:\\.\\d+)?)%");

    private static readonly System.Text.RegularExpressions.Regex DestinationRegex =
        new System.Text.RegularExpressions.Regex(@"Destination:\s*(.+)$");

    public double? ParsePercent(string line) {
        if (string.IsNullOrEmpty(line))
            return null;

        if (!line.Contains("[download]") || !line.Contains('%'))
            return null;

        var match = ProgressRegex.Match(line);
        if (match.Success && double.TryParse(match.Groups[1].Value,
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture,
            out var percent)) {
            return percent;
        }

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