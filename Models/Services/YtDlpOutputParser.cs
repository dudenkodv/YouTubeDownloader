using System.Text.RegularExpressions;

namespace YouTubeDownloader.Models.Services;

public class YtDlpOutputParser {
    private static readonly Regex ProgressRegex = new Regex(@"(\\d+(?:\\.\\d+)?)%");
    private static readonly Regex DestinationRegex = new Regex(@"Destination:\s*(.+)$");

    public double? ParsePercent(string line) {
        if (!line.Contains("[download]") || !line.Contains('%'))
            return null;

        var match = ProgressRegex.Match(line);
        if (match.Success && double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var percent))
            return percent;

        return null;
    }

    public string? ParseDestination(string line) {
        if (!line.Contains("[download]") || !line.Contains("Destination:"))
            return null;

        var match = DestinationRegex.Match(line);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }
}