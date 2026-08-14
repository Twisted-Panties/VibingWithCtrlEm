using System.Threading.Channels;
using Buttplug.Client;

namespace VibingWithCtrlEm.Services;

/// <summary>
/// Represents a single haptic command that will be placed on the pulse queue.
/// </summary>
internal record VibeCommand(string DeviceName, string Keyword, double Intensity, int DurationMs);

/// <summary>
/// Service wrapping ButtplugClient to manage connection to Intiface Central/Engine,
/// device discovery, and serialised vibration command execution.
/// Commands are queued and executed one at a time, in order.
/// </summary>
public class IntifaceService : IDisposable
{
    private ButtplugClient? _client;

    // ── Pulse queue ──────────────────────────────────────────────────────────
    // One unbounded channel; a single consumer Task works through it in order.
    private Channel<VibeCommand> _pulseQueue = Channel.CreateUnbounded<VibeCommand>();
    private CancellationTokenSource _pulseCts    = new();   // cancels the current Task.Delay
    private CancellationTokenSource _consumerCts = new();   // cancels the consumer loop
    private Task _consumerTask = Task.CompletedTask;

    // ── Events ───────────────────────────────────────────────────────────────
    public event Action<bool, string>? ConnectionStatusChanged;
    public event Action<ButtplugClientDevice>? DeviceAdded;
    public event Action<ButtplugClientDevice>? DeviceRemoved;

    /// <summary>
    /// Fired on the consumer thread when a vibration pulse actually begins
    /// executing (i.e. when it comes off the queue, not when it was enqueued).
    /// Parameters: keyword, deviceName, intensity, durationMs.
    /// </summary>
    public event Action<string, string, double, int>? CommandStarted;

    public bool IsConnected => _client?.Connected ?? false;
    public IReadOnlyList<ButtplugClientDevice> Devices => _client?.Devices ?? Array.Empty<ButtplugClientDevice>();

    // ── Connection ───────────────────────────────────────────────────────────

    public async Task ConnectAsync(string url)
    {
        if (IsConnected) return;

        _client = new ButtplugClient("Vibing With CtrlEm");
        _client.ServerDisconnect += OnServerDisconnect;
        _client.DeviceAdded      += OnDeviceAdded;
        _client.DeviceRemoved    += OnDeviceRemoved;

        var connector = new Buttplug.Client.ButtplugWebsocketConnector(new Uri(url));
        await _client.ConnectAsync(connector);

        StartConsumer();
        SafeInvokeStatusChanged(true, "Connected");

        try { await _client.StartScanningAsync(); }
        catch { /* safe to ignore — server may already be scanning */ }
    }

    public async Task DisconnectAsync()
    {
        if (_client is null) return;

        CancelPulseAndDrainQueue();
        await StopConsumerAsync();

        try
        {
            if (_client.Connected)
                await _client.DisconnectAsync();
        }
        catch (OperationCanceledException) { }
        catch (Exception) { }
        finally
        {
            CleanupClient();
            SafeInvokeStatusChanged(false, "Disconnected");
        }
    }

    /// <summary>
    /// Immediately cancels any running pulse, drains the queue, stops all devices,
    /// and disconnects. Designed for app shutdown — enforces a hard 3-second cap.
    /// </summary>
    public async Task ShutdownAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try
        {
            CancelPulseAndDrainQueue();
            await StopConsumerAsync();

            if (_client is not null && _client.Connected)
            {
                foreach (var device in _client.Devices)
                {
                    try { await device.VibrateAsync(0.0).WaitAsync(timeout.Token); }
                    catch { }
                }
                try { await _client.DisconnectAsync().WaitAsync(timeout.Token); }
                catch { }
            }
        }
        catch { }
        finally { CleanupClient(); }
    }

    // ── Queue management ─────────────────────────────────────────────────────

    /// <summary>
    /// Enqueue a vibration command. It will run after any currently executing
    /// command finishes.
    /// </summary>
    public void EnqueueVibratePulse(string deviceName, string keyword, double intensity, int durationMs)
    {
        if (_client is null || !IsConnected) return;
        _pulseQueue.Writer.TryWrite(new VibeCommand(deviceName, keyword, intensity, durationMs));
    }

    /// <summary>
    /// Send an immediate stop (intensity 0) that bypasses the queue.
    /// Use for Stop button / StopRunning — no queuing required.
    /// </summary>
    public async Task SendStopAsync(string deviceName)
    {
        if (_client is null || !IsConnected) return;
        var device = _client.Devices.FirstOrDefault(d =>
            d.Name.Equals(deviceName, StringComparison.OrdinalIgnoreCase));
        if (device is null) return;
        try { await device.VibrateAsync(0.0); }
        catch { }
    }

    /// <summary>
    /// Immediately cancel the current pulse's Task.Delay and discard every
    /// pending item in the queue. The consumer loop keeps running for future commands.
    /// </summary>
    public void CancelPulse()
    {
        CancelPulseAndDrainQueue();
    }

    private void CancelPulseAndDrainQueue()
    {
        // Cancel the Task.Delay inside the currently executing pulse
        try { _pulseCts.Cancel(); _pulseCts.Dispose(); } catch { }
        _pulseCts = new CancellationTokenSource();

        // Discard every pending (not-yet-started) command
        while (_pulseQueue.Reader.TryRead(out _)) { }
    }

    // ── Consumer loop ────────────────────────────────────────────────────────

    private void StartConsumer()
    {
        // Replace channel + consumer so we start fresh
        _pulseQueue   = Channel.CreateUnbounded<VibeCommand>();
        _consumerCts  = new CancellationTokenSource();
        _consumerTask = Task.Run(() => ConsumeLoopAsync(_consumerCts.Token));
    }

    private async Task StopConsumerAsync()
    {
        try
        {
            _consumerCts.Cancel();
            _pulseQueue.Writer.TryComplete();
            await _consumerTask.WaitAsync(TimeSpan.FromSeconds(1));
        }
        catch { }
    }

    private async Task ConsumeLoopAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var cmd in _pulseQueue.Reader.ReadAllAsync(ct))
            {
                await ExecutePulseAsync(cmd);

                // Brief inter-command pause so commands don't run back-to-back
                try { await Task.Delay(250, ct); }
                catch (OperationCanceledException) { break; }
            }
        }
        catch (OperationCanceledException) { }
        catch { }
    }

    private const int MaxDurationMs = 10_000;

    private async Task ExecutePulseAsync(VibeCommand cmd)
    {
        if (_client is null || !IsConnected) return;

        // Skip commands with no intensity or no duration — nothing useful to send.
        if (cmd.Intensity <= 0.0 || cmd.DurationMs <= 0) return;

        var device = _client.Devices.FirstOrDefault(d =>
            d.Name.Equals(cmd.DeviceName, StringComparison.OrdinalIgnoreCase));
        if (device is null) return;

        // Clamp duration to the configured maximum so a misconfigured entry
        // cannot hold the device on indefinitely.
        int durationMs = Math.Min(cmd.DurationMs, MaxDurationMs);

        // Notify the UI that this command is now actively running
        try { CommandStarted?.Invoke(cmd.Keyword, cmd.DeviceName, cmd.Intensity, durationMs); }
        catch { }

        // Capture the current pulse token before starting
        var token = _pulseCts.Token;
        try
        {
            await device.VibrateAsync(cmd.Intensity);
            await Task.Delay(durationMs, token);
            await device.VibrateAsync(0.0);
        }
        catch (OperationCanceledException)
        {
            // Pulse was cancelled mid-delay — send stop immediately
            try { await device.VibrateAsync(0.0); } catch { }
        }
        catch { }
    }

    // ── Buttplug event handlers ──────────────────────────────────────────────

    private void OnServerDisconnect(object? sender, EventArgs e)
    {
        CleanupClient();
        SafeInvokeStatusChanged(false, "Disconnected");
    }

    private void OnDeviceAdded(object? sender, DeviceAddedEventArgs e)
    {
        try { DeviceAdded?.Invoke(e.Device); }
        catch (OperationCanceledException) { }
        catch { }
    }

    private void OnDeviceRemoved(object? sender, DeviceRemovedEventArgs e)
    {
        try { DeviceRemoved?.Invoke(e.Device); }
        catch (OperationCanceledException) { }
        catch { }
    }

    private void SafeInvokeStatusChanged(bool connected, string message)
    {
        try { ConnectionStatusChanged?.Invoke(connected, message); }
        catch (OperationCanceledException) { }
        catch { }
    }

    private void CleanupClient()
    {
        if (_client is null) return;
        _client.ServerDisconnect -= OnServerDisconnect;
        _client.DeviceAdded      -= OnDeviceAdded;
        _client.DeviceRemoved    -= OnDeviceRemoved;
        try { _client.Dispose(); } catch { }
        _client = null;
    }

    // ── Legacy compatibility (test button uses this) ─────────────────────────

    /// <summary>
    /// Directly execute a pulse, bypassing the queue.
    /// Used by the Test Device button where immediate feedback is wanted.
    /// </summary>
    public async Task SendVibratePulseAsync(string deviceName, double intensity, int durationMs)
    {
        await ExecutePulseAsync(new VibeCommand(deviceName, "Testing device", intensity, durationMs));
    }

    // ── IDisposable ──────────────────────────────────────────────────────────

    public void Dispose()
    {
        try
        {
            CancelPulseAndDrainQueue();
            _consumerCts.Cancel();
            CleanupClient();
        }
        catch { }
    }
}
