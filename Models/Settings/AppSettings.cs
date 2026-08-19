namespace YouTubeDownloader.Models.Settings;

public class AppSettings {
    public bool EnableZapret { get; set; } = false;
    public string ZapretPath { get; set; } = "Tools/zapret/general.bat";
}