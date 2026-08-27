using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using VibingWithCtrlEm.Models;
using Application = System.Windows.Application;

namespace VibingWithCtrlEm.Services;

/// <summary>
/// Handles checking for GitHub updates, downloading new releases,
/// performing hot-swap replacement of the running executable, and restarting.
/// </summary>
public static class UpdateService
{
    public const string GitHubRepo = "Twisted-Panties/VibingWithCtrlEm";
    public const string ExeAssetName = "VibingWithCtrlEm.exe";

    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30),
        DefaultRequestHeaders =
        {
            { "User-Agent", "VibingWithCtrlEm-Updater" },
            { "Accept", "application/vnd.github.v3+json" }
        }
    };

    /// <summary>
    /// Gets the current running application version.
    /// </summary>
    public static Version CurrentVersion
    {
        get
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return version != null ? new Version(version.Major, version.Minor, Math.Max(0, version.Build)) : new Version(0, 9, 1);
        }
    }

    /// <summary>
    /// Clean up any leftover .old binary and temporary update files from previous updates.
    /// Safe to call on application startup.
    /// </summary>
    public static void CleanupOldVersion()
    {
        try
        {
            var currentExePath = GetCurrentExecutablePath();
            var backupExePath = currentExePath + ".old";
            if (File.Exists(backupExePath))
            {
                File.Delete(backupExePath);
            }

            var tempDir = Path.Combine(Path.GetTempPath(), "VibingWithCtrlEm_Update");
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
        catch
        {
            // Non-critical startup cleanup; ignore file lock / access errors
        }
    }

    /// <summary>
    /// Queries GitHub API for the latest release and checks if a newer version is available.
    /// Returns null if no newer version is found or if checking fails.
    /// </summary>
    public static async Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"https://api.github.com/repos/{GitHubRepo}/releases/latest";
            using var response = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = doc.RootElement;

            if (!root.TryGetProperty("tag_name", out var tagProp))
            {
                return null;
            }

            var tagName = tagProp.GetString() ?? string.Empty;
            var cleanTag = tagName.TrimStart('v', 'V').Trim();

            if (!Version.TryParse(cleanTag, out var latestVersion))
            {
                // Handle 2-part or partial version strings like "1.0"
                if (cleanTag.Contains('.') && Version.TryParse(cleanTag + ".0", out var fallbackVersion))
                {
                    latestVersion = fallbackVersion;
                }
                else
                {
                    return null;
                }
            }

            // Normalise versions to 3 components (Major.Minor.Build) for comparison
            var normLatest = new Version(latestVersion.Major, Math.Max(0, latestVersion.Minor), Math.Max(0, latestVersion.Build));
            var normCurrent = new Version(CurrentVersion.Major, Math.Max(0, CurrentVersion.Minor), Math.Max(0, CurrentVersion.Build));

            if (normLatest <= normCurrent)
            {
                return null;
            }

            // Look for VibingWithCtrlEm.exe in release assets
            string downloadUrl = string.Empty;
            long fileSize = 0;

            if (root.TryGetProperty("assets", out var assetsProp) && assetsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assetsProp.EnumerateArray())
                {
                    var name = asset.GetProperty("name").GetString() ?? string.Empty;
                    if (name.Equals(ExeAssetName, StringComparison.OrdinalIgnoreCase) ||
                        name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        downloadUrl = asset.GetProperty("browser_download_url").GetString() ?? string.Empty;
                        if (asset.TryGetProperty("size", out var sizeProp))
                        {
                            fileSize = sizeProp.GetInt64();
                        }
                        break;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(downloadUrl))
            {
                return null;
            }

            var releaseName = root.TryGetProperty("name", out var nameProp) ? (nameProp.GetString() ?? tagName) : tagName;
            var changelog = root.TryGetProperty("body", out var bodyProp) ? (bodyProp.GetString() ?? string.Empty) : string.Empty;

            return new UpdateInfo
            {
                Version = normLatest,
                TagName = tagName,
                ReleaseName = releaseName,
                Changelog = changelog,
                DownloadUrl = downloadUrl,
                FileSizeBytes = fileSize
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Downloads the update binary to a temporary file while reporting progress (0.0 to 1.0).
    /// </summary>
    public static async Task<string> DownloadUpdateAsync(string downloadUrl, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "VibingWithCtrlEm_Update");
        Directory.CreateDirectory(tempDir);
        var tempFilePath = Path.Combine(tempDir, "VibingWithCtrlEm_New.exe");

        if (File.Exists(tempFilePath))
        {
            try { File.Delete(tempFilePath); } catch { }
        }

        using var response = await HttpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

        var buffer = new byte[81920];
        long totalRead = 0;
        int bytesRead;

        while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            totalRead += bytesRead;

            if (totalBytes > 0 && progress != null)
            {
                progress.Report((double)totalRead / totalBytes);
            }
        }

        return tempFilePath;
    }

    /// <summary>
    /// Replaces the current running executable with the newly downloaded binary using the hot-swap
    /// rename method, launches the updated executable, and exits the current process.
    /// </summary>
    public static void ApplyUpdateAndRestart(string downloadedTempExePath)
    {
        var currentExePath = GetCurrentExecutablePath();
        var backupExePath = currentExePath + ".old";

        // 1. Remove previous backup if present
        if (File.Exists(backupExePath))
        {
            try { File.Delete(backupExePath); } catch { }
        }

        // 2. Rename current running exe to .old (Windows allows renaming running binaries)
        File.Move(currentExePath, backupExePath, overwrite: true);

        // 3. Move downloaded update into place
        File.Move(downloadedTempExePath, currentExePath, overwrite: true);

        // 4. Launch new executable
        var startInfo = new ProcessStartInfo
        {
            FileName = currentExePath,
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(currentExePath) ?? AppDomain.CurrentDomain.BaseDirectory
        };
        Process.Start(startInfo);

        // 5. Cleanly shut down current application
        Application.Current?.Dispatcher.Invoke(() =>
        {
            Application.Current.Shutdown();
        });

        Environment.Exit(0);
    }

    /// <summary>
    /// Resolves the absolute path to the main application executable.
    /// </summary>
    public static string GetCurrentExecutablePath()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(processPath) && File.Exists(processPath))
        {
            return processPath;
        }

        var mainModule = Process.GetCurrentProcess().MainModule?.FileName;
        if (!string.IsNullOrEmpty(mainModule) && File.Exists(mainModule))
        {
            return mainModule;
        }

        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VibingWithCtrlEm.exe");
    }
}
