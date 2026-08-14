using System.IO;
using System.Text.Json;
using VibingWithCtrlEm.Models;

namespace VibingWithCtrlEm.Services;

/// <summary>
/// Handles loading, saving, and generating default configuration for Vibing With CtrlEm.
/// Configuration is stored at %LOCALAPPDATA%\VibingWithCtrlEm\config.json.
/// </summary>
public static class ConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Gets the full path to config.json in %LOCALAPPDATA%\VibingWithCtrlEm\.
    /// Creates the directory if it does not exist.
    /// </summary>
    public static string GetConfigFilePath()
    {
        var appDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VibingWithCtrlEm");

        Directory.CreateDirectory(appDataFolder);
        return Path.Combine(appDataFolder, "config.json");
    }

    /// <summary>
    /// Load AppConfig from disk. If the file does not exist or is invalid,
    /// generates and saves a default AppConfig.
    /// </summary>
    public static AppConfig Load()
    {
        var path = GetConfigFilePath();

        if (File.Exists(path))
        {
            try
            {
                var json = File.ReadAllText(path);
                var loaded = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
                if (loaded is not null)
                {
                    return loaded;
                }
            }
            catch
            {
                // Fall back to default if reading/parsing fails
            }
        }

        var defaultConfig = CreateDefaultConfig();
        Save(defaultConfig);
        return defaultConfig;
    }

    /// <summary>
    /// Save an AppConfig instance to disk.
    /// </summary>
    public static void Save(AppConfig config)
    {
        var path = GetConfigFilePath();
        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(path, json);
    }

    /// <summary>
    /// Create the default AppConfig. First attempts to load from config.json in the application directory.
    /// If not found or invalid, falls back to the default configuration values.
    /// </summary>
    public static AppConfig CreateDefaultConfig()
    {
        var baseDirConfig = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
        if (File.Exists(baseDirConfig))
        {
            try
            {
                var json = File.ReadAllText(baseDirConfig);
                var loaded = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
                if (loaded is not null)
                {
                    return loaded;
                }
            }
            catch
            {
                // Fall back to default if reading/parsing fails
            }
        }

        return new AppConfig
        {
            IntifaceUrl = "ws://127.0.0.1:12345",
            LogFolderPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "CtrlEmClient", "Logs"),
            Commands =
            [
                new CommandMapping { Keyword = "changeWallpaper", Intensity = 0.50,  DurationMs = 750 },
                new CommandMapping { Keyword = "lockinput",       Intensity = 0.90,  DurationMs = 1000 },
                new CommandMapping { Keyword = "openPage",        Intensity = 0.40,  DurationMs = 500 },
                new CommandMapping { Keyword = "openshock",       Intensity = 0.65,  DurationMs = 1000 },
                new CommandMapping { Keyword = "pishock",         Intensity = 0.65,  DurationMs = 1000 },
                new CommandMapping { Keyword = "popupImage",      Intensity = 0.75,  DurationMs = 750 },
                new CommandMapping { Keyword = "popupSound",      Intensity = 0.25,  DurationMs = 500 },
                new CommandMapping { Keyword = "reactionTest",    Intensity = 0.85,  DurationMs = 2000 },
                new CommandMapping { Keyword = "screenBlank",     Intensity = 0.70,  DurationMs = 1000 },
                new CommandMapping { Keyword = "screenshot",      Intensity = 0.90,  DurationMs = 1250 },
                new CommandMapping { Keyword = "sendMessage",     Intensity = 0.40,  DurationMs = 500 },
                new CommandMapping { Keyword = "sendOrDelete",    Intensity = 0.65,  DurationMs = 800 },
                new CommandMapping { Keyword = "videoOverlay",    Intensity = 0.25,  DurationMs = 1500 },
                new CommandMapping { Keyword = "webcamCapture",   Intensity = 1.00,  DurationMs = 2500 },
                new CommandMapping { Keyword = "writeForMe",      Intensity = 0.50,  DurationMs = 1500 },
            ],
        };
    }
}
