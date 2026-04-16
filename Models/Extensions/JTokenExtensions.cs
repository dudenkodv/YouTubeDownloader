using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using YouTubeDownloader.Models.Entities;

namespace YouTubeDownloader.Models.Extensions;

public static class JTokenExtensions {
    public static VideoFormat? ToVideoFormat(this JToken fmt) {
        try {
            var vcodec = fmt["vcodec"]?.ToString() ?? "";
            var acodec = fmt["acodec"]?.ToString() ?? "";
            var formatNote = fmt["format_note"]?.ToString() ?? "";
            var formatId = fmt["format_id"]?.ToString() ?? "";
            var height = fmt["height"]?.Type != JTokenType.Null ? fmt["height"]?.Value<int?>() : null;
            var fps = fmt["fps"]?.Type != JTokenType.Null ? fmt["fps"]?.Value<double?>() : null;
            var tbr = fmt["tbr"]?.Type != JTokenType.Null ? fmt["tbr"]?.Value<int?>() : null;
            var filesize = fmt["filesize"]?.Type != JTokenType.Null ? fmt["filesize"]?.Value<long?>() : null;
            var resolution = fmt["resolution"]?.ToString() ?? "";
            var ext = fmt["ext"]?.ToString() ?? "";

            if (formatNote == "storyboard")
                return null;
            if (vcodec == "none" && acodec == "none")
                return null;

            return new VideoFormat {
                FormatId = formatId,
                Extension = ext,
                Resolution = resolution == "audio only" ? (height?.ToString() ?? "") : resolution,
                FormatNote = formatNote,
                Filesize = filesize,
                Tbr = tbr,
                Vcodec = vcodec,
                Acodec = acodec,
                Height = height,
                Fps = fps,
                IsAudioOnly = vcodec == "none"
            };
        } catch (Exception ex) {
            Log.Error(ex, "Parse error");
            return null;
        }
    }
}
