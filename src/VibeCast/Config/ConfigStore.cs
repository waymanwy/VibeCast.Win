using System.Text.Json;

namespace VibeCast.Config;

/// <summary>Loads and saves <see cref="AppConfig"/> as JSON under %APPDATA%\VibeCast.</summary>
public static class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static string Directory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VibeCast");

    public static string ConfigPath => Path.Combine(Directory, "config.json");

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                string json = File.ReadAllText(ConfigPath);
                AppConfig? cfg = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
                if (cfg is not null)
                {
                    bool needsResave = false;
                    if (string.IsNullOrEmpty(cfg.PairingToken))
                    {
                        // Config predates the pairing-token feature: the property
                        // initializer already generated one, just persist it.
                        needsResave = true;
                    }
                    if (needsResave) Save(cfg);
                    return cfg;
                }
            }
        }
        catch
        {
            // Corrupt file -> fall back to defaults rather than crashing at startup.
        }

        var fresh = new AppConfig();
        Save(fresh);
        return fresh;
    }

    public static void Save(AppConfig config)
    {
        System.IO.Directory.CreateDirectory(Directory);
        string json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(ConfigPath, json);
    }
}
