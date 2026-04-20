using Serilog;
using System.Diagnostics;
using System.IO;

public static class ZapretService {
    private static Process? _process;

    public static void Start() {
        //var zapretPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "zapret", "general.bat");
        //var zapretPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "general (ALT5).bat — ярлык");
        //var zapretPath = Path.Combine(desktopPath, "general (ALT5).bat — ярлык.lnk");
        var zapretPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "zapret", "general (ALT5).bat");
        if (!File.Exists(zapretPath)) {
            Log.Error("Zapret не найден: {Path}", zapretPath);
            return;
        }

        _process = Process.Start(new ProcessStartInfo {
            FileName = zapretPath,
            UseShellExecute = true,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });
    }

    public static void Stop() {
        if (_process != null && !_process.HasExited) {
            _process.Kill();
            _process.Dispose();
        }
    }
}