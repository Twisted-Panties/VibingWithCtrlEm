namespace VibingWithCtrlEm.Models;

/// <summary>
/// Root configuration model. Persisted to and loaded from config.json
/// in the application's local data folder.
/// </summary>
public class AppConfig
{
    /// <summary>WebSocket URL for Intiface Central / Intiface Engine.</summary>
    public string IntifaceUrl { get; set; } = "ws://127.0.0.1:12345";

    /// <summary>Path to the folder containing CtrlEm log files.</summary>
    public string LogFolderPath { get; set; } =
        System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "CtrlEmClient", "Logs");

    /// <summary>Whether to automatically check for updates on startup.</summary>
    public bool CheckForUpdates { get; set; } = true;

    /// <summary>Ordered list of command mappings loaded from config.</summary>
    public List<CommandMapping> Commands { get; set; } = [];
}

/// <summary>
/// A single log-keyword-to-haptic mapping.
/// </summary>
public class CommandMapping
{
    /// <summary>
    /// Keyword to match in the log file (case-insensitive substring match).
    /// </summary>
    public string Keyword { get; set; } = string.Empty;

    /// <summary>
    /// Vibration intensity sent to the device. Range: 0.0 (off) to 1.0 (max).
    /// </summary>
    public double Intensity { get; set; } = 0.5;

    /// <summary>
    /// How long to run the vibration command, in milliseconds.
    /// </summary>
    public int DurationMs { get; set; } = 500;
}
