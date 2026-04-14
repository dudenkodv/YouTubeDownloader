using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;

namespace YouTubeDownloader.Models.Services {
    internal static class ToolsValidatorWrap {
        public static void Check() {
            try {
                ToolsValidator.Validate();
            } catch (Exception ex) {
                MessageBox.Show(
                    $"Ошибка при запуске: {ex.Message}\n\nУбедитесь, что в папке Tools присутствуют файлы:\n- yt-dlp.exe\n- ffmpeg.exe\n- deno.exe",
                    "Критическая ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Log.Fatal(ex, "Ошибка валидации инструментов");

                Environment.Exit(1);
            }
        }
    }
}
