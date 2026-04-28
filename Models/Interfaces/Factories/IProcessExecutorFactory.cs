using System;
using System.Collections.Generic;
using System.Text;
using YouTubeDownloader.Models.Entities;

namespace YouTubeDownloader.Models.Interfaces.Factories;

public interface IProcessExecutorFactory {
    IProcessExecutor Create(ProcessExecutorTypeEnum type);
}
