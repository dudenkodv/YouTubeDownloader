using System.IO;
using Serilog;

namespace YouTubeDownloader.Services;

public static class ToolsValidator {
    private static readonly string[] RequiredTools = { "yt-dlp.exe", "ffmpeg.exe", "deno.exe" };

    public static void Validate() {
        var toolsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools");

        if (!Directory.Exists(toolsDir)) {
            var error = $"Папка Tools не существует: {toolsDir}";
            Log.Fatal(error);
            throw new DirectoryNotFoundException(error);
        }

        foreach (var tool in RequiredTools) {
            var toolPath = Path.Combine(toolsDir, tool);
            if (!File.Exists(toolPath)) {
                var error = $"Отсутствует необходимый файл: {toolPath}";
                Log.Fatal(error);
                throw new FileNotFoundException(error, toolPath);
            }
        }

        Log.Information("Все инструменты найдены в {ToolsDir}", toolsDir);
    }
}