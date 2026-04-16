using System;
using System.Collections.Generic;
using System.Text;
using YouTubeDownloader.Models.Interfaces;

namespace YouTubeDownloader.Models.Services;

public class TempFileManagerFactory : ITempFileManagerFactory {
    public ITempFileManager Create() => new TempFileManager();
}
