using YouTubeDownloader.Models.Entities;
using YouTubeDownloader.Models.DTOs;

namespace YouTubeDownloader.Models.Interfaces;

public interface IUpdateService : IDisposable {
    Task<ResultDto<UpdateInfo>> CheckYtDlpUpdateAsync();
    Task<ResultDto<UpdateInfo>> CheckDenoUpdateAsync();
    Task<ResultDto<UpdateInfo>> CheckFfmpegUpdateAsync();
    Task<ResultDto<bool>> DownloadAndUpdateToolAsync(UpdateInfo updateInfo, IProgress<int>? progress = null);
}