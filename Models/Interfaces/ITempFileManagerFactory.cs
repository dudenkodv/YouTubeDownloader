using System;
using System.Collections.Generic;
using System.Text;

namespace YouTubeDownloader.Models.Interfaces;

public interface ITempFileManagerFactory {
    ITempFileManager Create();
}
