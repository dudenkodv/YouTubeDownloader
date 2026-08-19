using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using System.IO;
using System.Net.Http;
using YouTubeDownloader.Models.Interfaces;
using YouTubeDownloader.Models.Interfaces.Factories;
using YouTubeDownloader.Models.ProgressTasks;
using YouTubeDownloader.Models.Services;
using YouTubeDownloader.Models.Services.Factories;
using YouTubeDownloader.Models.Services.Strategies;
using YouTubeDownloader.Models.Services.UpdateCheckers;
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

        // Фабрика задач
        services.AddSingleton<IProgressTaskFactory, ProgressTaskFactory>();

        // Стратегии скачивания
        services.AddSingleton<FormatDownloadStrategy>();
        services.AddSingleton<SimpleDownloadStrategy>();
        services.AddSingleton<IDownloadStrategyFactory, DownloadStrategyFactory>();

        services.AddSingleton<ProcessExecutor>();
        services.AddSingleton<ProcessKillExecutor>();
        services.AddSingleton<IProcessExecutorFactory, ProcessExecutorFactory>();

        // YouTubeService
        services.AddSingleton<IYouTubeService, YouTubeService>();

        // ViewModels
        services.AddTransient<MainViewModel>();

        // Views
        services.AddTransient<MainWindow>();

        // ProcessManager (новый сервис)
        services.AddSingleton<IProcessManager, ProcessManager>();

        // UpdateService
        services.AddSingleton<HttpClient>();
        services.AddSingleton<IUpdateChecker, YtDlpUpdateChecker>();
        services.AddSingleton<IUpdateChecker, DenoUpdateChecker>();
        services.AddSingleton<IUpdateChecker, FfmpegUpdateChecker>();
        services.AddSingleton<IUpdateService, UpdateService>();

        return services;
    }
}