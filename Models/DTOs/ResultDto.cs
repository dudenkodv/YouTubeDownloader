using System;
using System.Collections.Generic;
using System.Text;

namespace YouTubeDownloader.Models.DTOs; 
public record ResultDto<T>(bool isSuccess, string message, T data) {}
