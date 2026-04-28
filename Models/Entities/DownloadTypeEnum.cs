using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace YouTubeDownloader.Models.Entities;

public enum DownloadTypeEnum : int{
    [Description("Скачивание аудио")]
    Audio = 0,
    [Description("Скачивание видео")]
    Video = 1,    
}
