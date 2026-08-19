using CliWrap;
using CliWrap.Buffered;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.IO;
using YouTubeDownloader.Models.DTOs;
using YouTubeDownloader.Models.Interfaces;

public class ZapretService {
    private readonly IProcessExecutor _processExecutor;
    private readonly ILogger<ZapretService> _logger;
    private Process? _process;

    public ZapretService(IProcessExecutor processExecutor, ILogger<ZapretService> logger) {
        _processExecutor = processExecutor;
        _logger = logger;
    }

    public async Task<ResultDto<bool>> StartAsync() {
        try {
            var zapretPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Tools",
                "zapret",
                "general (ALT5).bat"
            );

            if (!File.Exists(zapretPath)) {
                var error = $"Zapret не найден: {zapretPath}";
                _logger.LogError(error);
                return new ResultDto<bool>(false, error, false);
            }

            // Проверяем, не запущен ли уже
            if (_process != null && !_process.HasExited) {
                _logger.LogWarning("Zapret уже запущен");
                return new ResultDto<bool>(true, "Zapret уже запущен", true);
            }

            _logger.LogInformation("Запуск Zapret: {Path}", zapretPath);

            _process = Process.Start(new ProcessStartInfo {
                FileName = zapretPath,
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });

            if (_process == null) {
                var error = "Не удалось запустить процесс Zapret";
                _logger.LogError(error);
                return new ResultDto<bool>(false, error, false);
            }

            // Даём процессу время на запуск
            await Task.Delay(500);

            if (_process.HasExited) {
                var error = $"Zapret завершился сразу после запуска. ExitCode: {_process.ExitCode}";
                _logger.LogError(error);
                return new ResultDto<bool>(false, error, false);
            }

            _logger.LogInformation("Zapret успешно запущен");
            return new ResultDto<bool>(true, "Zapret запущен", true);
        } catch (Exception ex) {
            _logger.LogError(ex, "Ошибка запуска Zapret");
            return new ResultDto<bool>(false, ex.Message, false);
        }
    }

    public ResultDto<bool> Stop() {
        try {
            if (_process == null || _process.HasExited) {
                _logger.LogWarning("Zapret не запущен или уже завершён");
                return new ResultDto<bool>(true, "Zapret не был запущен", true);
            }

            _logger.LogInformation("Остановка Zapret");

            try {
                _process.Kill();
                _process.WaitForExit(1000);
                _process.Dispose();
                _process = null;

                _logger.LogInformation("Zapret успешно остановлен");
                return new ResultDto<bool>(true, "Zapret остановлен", true);
            } catch (Exception ex) {
                _logger.LogError(ex, "Ошибка при остановке Zapret");
                return new ResultDto<bool>(false, ex.Message, false);
            }
        } catch (Exception ex) {
            _logger.LogError(ex, "Ошибка остановки Zapret");
            return new ResultDto<bool>(false, ex.Message, false);
        }
    }

    public bool IsRunning() {
        return _process != null && !_process.HasExited;
    }
}