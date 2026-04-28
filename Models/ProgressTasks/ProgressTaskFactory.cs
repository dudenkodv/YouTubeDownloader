using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using YouTubeDownloader.Models.Interfaces;
using YouTubeDownloader.Models.Interfaces.Factories;

namespace YouTubeDownloader.Models.ProgressTasks;

public class ProgressTaskFactory : IProgressTaskFactory {
    private readonly IYouTubeService _youtubeService;
    private readonly ILoggerFactory _loggerFactory;

    public ProgressTaskFactory(IYouTubeService youtubeService, ILoggerFactory loggerFactory) {
        _youtubeService = youtubeService;
        _loggerFactory = loggerFactory;
    }

    public ProgressTask Create(TaskParameters parameters) {
        return parameters.Type switch {
            TaskType.LoadInfo => new LoadInfoTask(
                _youtubeService,
                _loggerFactory.CreateLogger<LoadInfoTask>(),
                parameters.Url,
                parameters.OnFormatsLoaded!),

            TaskType.Download => new DownloadTask(
                _youtubeService,
                _loggerFactory.CreateLogger<DownloadTask>()) {
                Name = parameters.Name,
                Url = parameters.Url,
                FormatId = parameters.FormatId,
                OutputPath = parameters.OutputPath,
                Status = "Ожидание"
            },

            _ => throw new NotSupportedException($"Task type {parameters.Type} not supported")
        };
    }
}
