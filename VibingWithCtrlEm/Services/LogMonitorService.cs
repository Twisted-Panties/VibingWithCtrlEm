using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using VibingWithCtrlEm.Models;

namespace VibingWithCtrlEm.Services;

/// <summary>
/// Service that continuously monitors log files in a target directory.
/// Locates the most recently written .log file and tails it as lines are appended.
/// </summary>
public class LogMonitorService : IDisposable
{
    private CancellationTokenSource? _cts;
    private Task? _monitoringTask;
    private readonly List<CommandMapping> _commandMappings = [];

    public event Action<string>? ActiveFileChanged;
    // lineText, matchedMapping (null if not matched)
    public event Action<string, CommandMapping?>? LineProcessed;

    public void UpdateCommands(IEnumerable<CommandMapping> commands)
    {
        lock (_commandMappings)
        {
            _commandMappings.Clear();
            _commandMappings.AddRange(commands.Where(c => !string.IsNullOrWhiteSpace(c.Keyword)));
        }
    }

    public void Start(string logFolderPath, IEnumerable<CommandMapping> commands)
    {
        Stop();
        UpdateCommands(commands);

        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _monitoringTask = Task.Run(() => MonitorLoop(logFolderPath, token), token);
    }

    public void Stop()
    {
        if (_cts is not null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }
        _monitoringTask = null;
    }

    private async Task MonitorLoop(string folderPath, CancellationToken token)
    {
        string? currentFilePath = null;
        FileStream? stream = null;
        StreamReader? reader = null;

        try
        {
            while (!token.IsCancellationRequested)
            {
                if (!Directory.Exists(folderPath))
                {
                    await Task.Delay(1000, token);
                    continue;
                }

                var latestFile = new DirectoryInfo(folderPath)
                    .GetFiles("*.log")
                    .OrderByDescending(f => f.LastWriteTimeUtc)
                    .FirstOrDefault();

                if (latestFile is null)
                {
                    await Task.Delay(1000, token);
                    continue;
                }

                // Switch to new latest log file if found
                if (currentFilePath != latestFile.FullName)
                {
                    reader?.Dispose();
                    stream?.Dispose();

                    currentFilePath = latestFile.FullName;
                    ActiveFileChanged?.Invoke(latestFile.Name);

                    // Open with FileShare.ReadWrite so active log writers are not blocked
                    stream = new FileStream(currentFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    reader = new StreamReader(stream, Encoding.UTF8);

                    // Start at the end of existing file content to catch new lines appended from startup onwards
                    stream.Seek(0, SeekOrigin.End);
                }

                if (reader is not null)
                {
                    string? line;
                    while ((line = await reader.ReadLineAsync(token)) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        var matchedMapping = FindMatchingCommand(line);
                        LineProcessed?.Invoke(line, matchedMapping);
                    }
                }

                await Task.Delay(250, token);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception) { }
        finally
        {
            reader?.Dispose();
            stream?.Dispose();
        }
    }

    /// <summary>
    /// Checks if a log line matches any configured command keyword exactly as a distinct word/token.
    /// </summary>
    private CommandMapping? FindMatchingCommand(string line)
    {
        lock (_commandMappings)
        {
            foreach (var cmd in _commandMappings)
            {
                if (string.IsNullOrWhiteSpace(cmd.Keyword)) continue;

                // Exact word match (bounded by non-word characters or string boundaries)
                string pattern = $@"\b{Regex.Escape(cmd.Keyword.Trim())}\b";
                if (Regex.IsMatch(line, pattern, RegexOptions.IgnoreCase))
                {
                    return cmd;
                }
            }
        }
        return null;
    }

    public void Dispose()
    {
        Stop();
    }
}
