using System.IO;
using System.Text.Json;
using YouTubeDownloader.Models.Settings;

namespace YouTubeDownloader.Models.Services;

public interface ISettingsService {
    AppSettings Load();
    void Save(AppSettings settings);
    bool EnableZapret { get; set; }
}

public class SettingsService : ISettingsService {
    private readonly string _settingsPath;
    private AppSettings _settings;

    public SettingsService() {
        _settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Settings", "appsettings.json");
        _settings = Load();
    }

    public bool EnableZapret {
        get => _settings.EnableZapret;
        set {
            if (_settings.EnableZapret != value) {
                _settings.EnableZapret = value;
                Save(_settings);
            }
        }
    }

    public AppSettings Load() {
        try {
            if (File.Exists(_settingsPath)) {
                var json = File.ReadAllText(_settingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        } catch {
            // Если файл битый — создаём новый
        }

        var defaultSettings = new AppSettings();
        Save(defaultSettings);
        return defaultSettings;
    }

    public void Save(AppSettings settings) {
        var dir = Path.GetDirectoryName(_settingsPath);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir!);

        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_settingsPath, json);
    }
}