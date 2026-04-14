using Newtonsoft.Json;

namespace YouTubeDownloader.Models.Entities;

public class VideoFormat {
    [JsonProperty("format_id")]
    public string FormatId { get; set; } = string.Empty;

    [JsonProperty("ext")]
    public string Extension { get; set; } = string.Empty;

    [JsonProperty("resolution")]
    public string Resolution { get; set; } = string.Empty;

    [JsonProperty("format_note")]
    public string FormatNote { get; set; } = string.Empty;

    [JsonProperty("filesize")]
    public long? Filesize { get; set; }

    [JsonProperty("tbr")]
    public int? Tbr { get; set; }

    [JsonProperty("vcodec")]
    public string Vcodec { get; set; } = string.Empty;

    [JsonProperty("acodec")]
    public string Acodec { get; set; } = string.Empty;

    [JsonProperty("height")]
    public int? Height { get; set; }

    [JsonProperty("width")]
    public int? Width { get; set; }

    [JsonProperty("fps")]
    public double? Fps { get; set; }

    [JsonIgnore]
    public bool IsAudioOnly { get; set; }

    public string DisplayName {
        get {
            if (!string.IsNullOrEmpty(Resolution) && Resolution != "audio only") {
                var codec = "";
                if (Vcodec?.Contains("av01") == true)
                    codec = " AV1";
                else if (Vcodec?.Contains("vp9") == true)
                    codec = " VP9";
                else if (Vcodec?.Contains("avc") == true)
                    codec = " H.264";

                var fpsInfo = (Fps.HasValue && Fps > 30) ? $" {Fps.Value:F0}fps" : "";
                return $"{Resolution} - {Extension.ToUpper()}{codec}{fpsInfo}";
            } else {
                return $"{Tbr?.ToString() ?? "?"}kbps - MP3";
            }
        }
    }
}