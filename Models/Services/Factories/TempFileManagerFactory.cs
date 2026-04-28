using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using YouTubeDownloader.Models.Interfaces;
using YouTubeDownloader.Models.Interfaces.Factories;

namespace YouTubeDownloader.Models.Services.Factories;

public class TempFileManagerFactory : ITempFileManagerFactory {
    private readonly ILoggerFactory _loggerFactory;
    public TempFileManagerFactory(ILoggerFactory loggerFactory) {
        _loggerFactory = loggerFactory;
    }
    public ITempFileManager Create() => new TempFileManager(_loggerFactory.CreateLogger<TempFileManager>());
}
