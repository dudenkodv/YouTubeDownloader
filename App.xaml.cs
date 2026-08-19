using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using System.IO;
using System.Windows;
using YouTubeDownloader.Models.Extensions;
using YouTubeDownloader.Models.Services;

namespace YouTubeDownloader;

public partial class App : Application {
    private ServiceProvider? _serviceProvider;
    protected override void OnStartup(StartupEventArgs e) {
        base.OnStartup(e);

        var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
        if (!Directory.Exists(logDir))
            Directory.CreateDirectory(logDir);

        foreach (var file in Directory.GetFiles(logDir)) {
            File.Delete(file);
        }

        var today = DateTime.Now.ToString("yyyy_MM_dd");

        Log.Logger = new LoggerConfiguration().MinimumLevel.Debug()
            .WriteTo.Logger(lc => lc.Filter.ByIncludingOnly(ev => ev.Level == LogEventLevel.Debug)
                .WriteTo.File(Path.Combine(logDir, $"debug_{today}.log")))
            .WriteTo.Logger(lc => lc.Filter.ByIncludingOnly(ev => ev.Level == LogEventLevel.Information)
                .WriteTo.File(Path.Combine(logDir, $"info_{today}.log")))
            .WriteTo.Logger(lc => lc.Filter.ByIncludingOnly(ev => ev.Level == LogEventLevel.Error)
                .WriteTo.File(Path.Combine(logDir, $"error_{today}.log")))
            .CreateLogger();

        ToolsValidatorWrap.Check();
        //todo вынести в настройку
        //ZapretService.Start();

        var services = new ServiceCollection();
        services.AddYouTubeServices();

        _serviceProvider = services.BuildServiceProvider();

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e) {
        ZapretService.Stop();
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}