using System;
using System.Collections.Generic;
using System.Text;
using YouTubeDownloader.Models.Entities;

namespace YouTubeDownloader.Models.ProgressTasks;

public class TaskParameters {
    public TaskType Type { get; set; }
    public string Url { get; set; } = string.Empty;
    public string FormatId { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Action<List<VideoFormat>>? OnFormatsLoaded { get; set; }
}
