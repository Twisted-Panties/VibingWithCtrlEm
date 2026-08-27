namespace VibingWithCtrlEm.Models;

/// <summary>
/// Contains metadata about an available application update discovered on GitHub.
/// </summary>
public class UpdateInfo
{
    /// <summary>Parsed semantic version of the update.</summary>
    public Version Version { get; set; } = new(0, 0, 0);

    /// <summary>GitHub tag name (e.g. "v0.9.2").</summary>
    public string TagName { get; set; } = string.Empty;

    /// <summary>GitHub release title/name.</summary>
    public string ReleaseName { get; set; } = string.Empty;

    /// <summary>Markdown release notes / changelog.</summary>
    public string Changelog { get; set; } = string.Empty;

    /// <summary>Direct download URL for the VibingWithCtrlEm.exe binary asset.</summary>
    public string DownloadUrl { get; set; } = string.Empty;

    /// <summary>Size of the release asset in bytes.</summary>
    public long FileSizeBytes { get; set; }
}
