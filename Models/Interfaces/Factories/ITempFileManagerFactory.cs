using System;
using System.Collections.Generic;
using System.Text;

namespace YouTubeDownloader.Models.Interfaces.Factories;

public interface ITempFileManagerFactory {
    ITempFileManager Create();
}
