using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using System.IO;
using YouTubeDownloader.Models.Interfaces;
using YouTubeDownloader.Models.Services;
using YouTubeDownloader.Models.Settings;
using YouTubeDownloader.ViewModels;
using YouTubeDownloader.Views;

namespace YouTubeDownloader.Models.Extensions;

public static class ServiceCollectionExtensions {
    public static IServiceCollection AddYouTubeServices(this IServiceCollection services) {
        // Настройки
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;

        var ytDlpSettings = new YtDlpSettings {
            YtDlpPath = Path.Combine(baseDir, "Tools", "yt-dlp.exe"),
            FfmpegPath = Path.Combine(baseDir, "Tools", "ffmpeg.exe"),
            DownloadTimeoutSeconds = 300,
            MaxRetries = 3
        };

        var ffmpegSettings = new FfmpegSettings {
            DefaultMp3Bitrate = 192,
            OutputFormat = "mp4"
        };

        services.AddSingleton(ytDlpSettings);
        services.AddSingleton(ffmpegSettings);

        // Логирование через Serilog (уже настроено в App.xaml.cs)
        services.AddLogging(builder => {
            builder.ClearProviders();
            builder.AddSerilog(Log.Logger, dispose: true);
        });

        // Core сервисы
        services.AddSingleton<IProcessExecutor, ProcessExecutor>();
        services.AddSingleton<IYtDlpOutputParser, YtDlpOutputParser>();
        services.AddSingleton<ITempFileManagerFactory, TempFileManagerFactory>();
        services.AddSingleton<IFfmpegConverter, FfmpegConverter>();

        // YouTubeService
        services.AddTransient<IYouTubeService, YouTubeService>();

        // ViewModels
        services.AddTransient<MainViewModel>();

        // Views
        services.AddTransient<MainWindow>();

        return services;
    }
}