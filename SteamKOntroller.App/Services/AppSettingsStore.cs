using System.Text.Json;

namespace SteamKOntroller.App.Services;

internal sealed class AppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };
    private static readonly AppJsonSerializerContext JsonContext = new(JsonOptions);

    private AppSettingsStore(string path, AppSettings settings)
    {
        Path = path;
        Settings = settings;
    }

    public string Path { get; }
    public AppSettings Settings { get; }

    public static AppSettingsStore LoadDefault()
    {
        var settingsDir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SteamKOntroller",
            "App");
        var path = System.IO.Path.Combine(settingsDir, "settings.json");

        if (!File.Exists(path))
        {
            return new AppSettingsStore(path, new AppSettings());
        }

        try
        {
            var settings = JsonSerializer.Deserialize(File.ReadAllText(path), JsonContext.AppSettings)
                ?? new AppSettings();
            settings.LogRetentionDays = ClampRetentionDays(settings.LogRetentionDays);
#if !DEBUG
            settings.SensitiveInputLoggingEnabled = false;
#endif
            return new AppSettingsStore(path, settings);
        }
        catch (JsonException)
        {
            return new AppSettingsStore(path, new AppSettings());
        }
        catch (IOException)
        {
            return new AppSettingsStore(path, new AppSettings());
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        File.WriteAllText(Path, JsonSerializer.Serialize(Settings, JsonContext.AppSettings));
    }

    public static int ClampRetentionDays(int days) => Math.Clamp(days, 1, 90);
}
