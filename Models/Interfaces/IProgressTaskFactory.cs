using System;
using System.Collections.Generic;
using System.Text;
using YouTubeDownloader.Models.ProgressTasks;

namespace YouTubeDownloader.Models.Interfaces;

public interface IProgressTaskFactory {
    ProgressTask Create(TaskParameters parameters);
}
