using System.IO;
using System.Windows;
using VibingWithCtrlEm.Models;
using VibingWithCtrlEm.Services;
using VibingWithCtrlEm.Views;
// Disambiguate: both WPF and WinForms expose 'Application'.
using Application = System.Windows.Application;

namespace VibingWithCtrlEm;

/// <summary>
/// Main application window for Vibing With CtrlEm.
/// Watches a CtrlEm log file and translates matched events into
/// haptic commands sent via the Buttplug.io protocol to Intiface Central.
/// </summary>
public partial class MainWindow : Window
{
    // ─────────────────────────────────────────────
    // State
    // ─────────────────────────────────────────────

    private bool _isDarkMode = false;
    private bool _isExiting = false;
    private bool _isRunning = false;
    private bool _trayBalloonShown = false;
    private AppConfig _config = null!;
    private NotifyIcon _trayIcon = null!;
    private ToolStripMenuItem _trayMenuStart = null!;
    private ToolStripMenuItem _trayMenuStop = null!;
    private readonly LogMonitorService _logMonitor = new();
    private readonly IntifaceService _intifaceService = new();

    // ─────────────────────────────────────────────
    // Startup
    // ─────────────────────────────────────────────

    public MainWindow()
    {
        InitializeComponent();

        // Clean up any leftover update temporary files / previous .old binary
        UpdateService.CleanupOldVersion();

        // Window.Icon set here rather than in XAML to avoid a TypeConverter
        // exception that WPF throws when resolving ICO paths at parse time.
        try
        {
            var pngUri = new Uri("pack://application:,,,/Assets/app-icon.png");
            Icon = System.Windows.Media.Imaging.BitmapFrame.Create(
                       Application.GetResourceStream(pngUri)!.Stream);
        }
        catch { /* non-critical: fall back to default WPF icon */ }

        InitialiseTrayIcon();
        LoadConfigAndPopulateUi();

        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_config.CheckForUpdates)
        {
            _ = CheckForUpdatesInBackgroundAsync();
        }
    }

    private async Task CheckForUpdatesInBackgroundAsync()
    {
        try
        {
            // Brief pause so main window finishes layout rendering smoothly
            await Task.Delay(1500);

            if (_isExiting) return;

            var updateInfo = await UpdateService.CheckForUpdateAsync();
            if (updateInfo is not null && !_isExiting)
            {
                var updateWin = new UpdateWindow(updateInfo)
                {
                    Owner = this
                };
                updateWin.ShowDialog();
            }
        }
        catch
        {
            // Background check failure should not disrupt the application
        }
    }

    /// <summary>
    /// Create the system-tray icon from the embedded ICO resource.
    /// Double-click restores the window; right-click shows Restore / Exit.
    /// </summary>
    private void InitialiseTrayIcon()
    {
        Icon? trayIconImage = null;

        try
        {
            var pngUri = new Uri("pack://application:,,,/Assets/app-icon.png");
            var stream = Application.GetResourceStream(pngUri)?.Stream;
            if (stream is not null)
            {
                using var bmp = new Bitmap(stream);
                IntPtr hIcon = bmp.GetHicon();
                trayIconImage = System.Drawing.Icon.FromHandle(hIcon);
            }
        }
        catch
        {
            trayIconImage = SystemIcons.Application;
        }

        _trayIcon = new NotifyIcon
        {
            Text = "Vibing With CtrlEm",
            Icon = trayIconImage ?? SystemIcons.Application
        };

        _trayMenuStart = new ToolStripMenuItem("Start")
        {
            Enabled = false
        };
        _trayMenuStart.Click += (_, _) => Dispatcher.Invoke(() => StartRunning());

        _trayMenuStop = new ToolStripMenuItem("Stop")
        {
            Enabled = false
        };
        _trayMenuStop.Click += (_, _) => Dispatcher.Invoke(() => StopRunning());

        var menu = new ContextMenuStrip();
        menu.Items.Add("Restore", null, (_, _) => RestoreWindow());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_trayMenuStart);
        menu.Items.Add(_trayMenuStop);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApplication());

        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.Click += (_, e) =>
        {
            if (e is MouseEventArgs me && me.Button == MouseButtons.Left)
            {
                RestoreWindow();
            }
        };
        _trayIcon.DoubleClick += (_, _) => RestoreWindow();
        _trayIcon.Visible = true;
    }

    private void RestoreWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    /// <summary>
    /// Load (or create) config.json and bind every UI element to its value.
    /// </summary>
    private void LoadConfigAndPopulateUi()
    {
        _config = ConfigService.Load();

        // ── Apply saved theme ────────────────────────────────
        ApplyTheme(_config.DarkMode);

        // ── Bottom status: App version ────────────────────────
        var v = UpdateService.CurrentVersion;
        TxtAppVersion.Text = $"v{v.Major}.{v.Minor}.{v.Build}";

        // ── Left panel: Intiface connection ──────────────────
        TxtIntifaceUrl.Text = _config.IntifaceUrl;

        // ── Middle panel: Log file ────────────────────────────
        TxtLogFolder.Text = _config.LogFolderPath;
        // TxtCurrentLogFile is a runtime value — populated when watching starts.

        // ── Right panel: Command list ─────────────────────────
        // The ListView column templates bind to Keyword / Intensity / DurationMs
        // via the standard WPF DataTemplate bindings already declared in XAML.
        LvCommands.ItemsSource = _config.Commands;

        // ── Start log monitoring ──────────────────────────────
        _logMonitor.ActiveFileChanged += fileName =>
        {
            SafeInvoke(() => TxtCurrentLogFile.Text = fileName);
        };

        _logMonitor.LineProcessed += (lineText, matchedMapping) =>
        {
            SafeInvoke(() =>
            {
                if (matchedMapping is not null && _isRunning
                    && CmbDevices.SelectedItem is string deviceName
                    && !string.IsNullOrEmpty(deviceName))
                {
                    // Enqueue command — TxtLastCommand updated by CommandStarted when it actually runs
                    _intifaceService.EnqueueVibratePulse(deviceName, matchedMapping.Keyword, matchedMapping.Intensity, matchedMapping.DurationMs);
                }
            });
        };

        // ── CommandStarted: update Running Command and Commands Executed count when pulse begins
        _intifaceService.CommandStarted += (keyword, deviceName, intensity, durationMs) =>
        {
            SafeInvoke(() =>
            {
                double durationSecs = durationMs / 1000.0;
                string label = string.IsNullOrEmpty(keyword) ? string.Empty : $"{keyword} at ";
                TxtLastCommand.Text = $"{label}{intensity:P0} for {durationSecs:0.##}s on {deviceName}";

                // Increment Commands Executed counter only when a real keyword-triggered pulse starts
                if (!string.IsNullOrEmpty(keyword)
                    && int.TryParse(TxtLinesMatched.Text, out int current))
                {
                    TxtLinesMatched.Text = (current + 1).ToString();
                }
            });
        };

        // ── Intiface Service Callbacks ────────────────────────
        _intifaceService.ConnectionStatusChanged += (connected, statusMsg) =>
        {
            SafeInvoke(() =>
            {
                TxtConnectionStatus.Text = statusMsg;
                var brushKey = connected ? "StatusConnectedBrush" : "StatusDisconnectedBrush";
                EllipseStatus.Fill = (System.Windows.Media.Brush)Application.Current.Resources[brushKey];

                BtnConnect.IsEnabled = !connected;
                BtnDisconnect.IsEnabled = connected;
                CmbDevices.IsEnabled = connected && !_isRunning;
                bool canStart = connected && CmbDevices.SelectedItem != null && !_isRunning;
                BtnTestDevice.IsEnabled = canStart;
                BtnStart.IsEnabled = canStart;
                _trayMenuStart.Enabled = canStart;
                // Stop: only enabled while running — don't toggle it here (StopRunning will handle it)

                if (!connected && _isRunning)
                {
                    StopRunning();
                }
            });
        };

        _intifaceService.DeviceAdded += device =>
        {
            SafeInvoke(() =>
            {
                if (!CmbDevices.Items.Contains(device.Name))
                {
                    CmbDevices.Items.Add(device.Name);
                }
                if (CmbDevices.SelectedIndex == -1 && CmbDevices.Items.Count > 0)
                {
                    CmbDevices.SelectedIndex = 0;
                }
                BtnTestDevice.IsEnabled = _intifaceService.IsConnected && CmbDevices.SelectedItem != null && !_isRunning;
                bool canStart2 = _intifaceService.IsConnected && CmbDevices.SelectedItem != null && !_isRunning;
                BtnStart.IsEnabled = canStart2;
                _trayMenuStart.Enabled = canStart2;
            });
        };

        _intifaceService.DeviceRemoved += device =>
        {
            SafeInvoke(() =>
            {
                CmbDevices.Items.Remove(device.Name);
                if (CmbDevices.SelectedIndex == -1 && CmbDevices.Items.Count > 0)
                {
                    CmbDevices.SelectedIndex = 0;
                }
                BtnTestDevice.IsEnabled = _intifaceService.IsConnected && CmbDevices.SelectedItem != null && !_isRunning;
                bool canStart3 = _intifaceService.IsConnected && CmbDevices.SelectedItem != null && !_isRunning;
                BtnStart.IsEnabled = canStart3;
                _trayMenuStart.Enabled = canStart3;
            });
        };

        StartLogMonitoring();
    }

    private void SafeInvoke(Action action)
    {
        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;
        try
        {
            Dispatcher.Invoke(action);
        }
        catch (OperationCanceledException) { }
        catch { }
    }

    private void SafeInvokeAsync(Func<Task> action)
    {
        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;
        try
        {
            Dispatcher.Invoke(async () =>
            {
                try
                {
                    await action();
                }
                catch (OperationCanceledException) { }
                catch { }
            });
        }
        catch (OperationCanceledException) { }
        catch { }
    }

    private void StartLogMonitoring()
    {
        _logMonitor.Start(_config.LogFolderPath, _config.Commands);
    }

    // ─────────────────────────────────────────────
    // Theme Toggle
    // ─────────────────────────────────────────────

    private void ApplyTheme(bool isDark)
    {
        _isDarkMode = isDark;

        var themeUri = _isDarkMode
            ? new Uri("Themes/Dark.xaml",  UriKind.Relative)
            : new Uri("Themes/Light.xaml", UriKind.Relative);

        var newTheme = new ResourceDictionary { Source = themeUri };
        Application.Current.Resources.MergedDictionaries.Clear();
        Application.Current.Resources.MergedDictionaries.Add(newTheme);

        BtnToggleTheme.Content = _isDarkMode ? "☀  Light Mode" : "🌙  Dark Mode";
    }

    private void BtnToggleTheme_Click(object sender, RoutedEventArgs e)
    {
        ApplyTheme(!_isDarkMode);
        _config.DarkMode = _isDarkMode;
    }

    private void BtnFeedback_Click(object sender, RoutedEventArgs e)
    {
        var feedbackWindow = new FeedbackWindow
        {
            Owner = this
        };
        feedbackWindow.ShowDialog();
    }

    // ─────────────────────────────────────────────
    // Intiface Connection Panel
    // ─────────────────────────────────────────────

    /// <summary>
    /// When the user tabs away from / clicks off the URL field,
    /// persist the new value to config.json immediately.
    /// </summary>
    private void TxtIntifaceUrl_LostFocus(object sender, RoutedEventArgs e)
    {
        var newUrl = TxtIntifaceUrl.Text.Trim();
        if (newUrl == _config.IntifaceUrl) return;

        _config.IntifaceUrl = newUrl;
        ConfigService.Save(_config);
    }

    private async void BtnConnect_Click(object sender, RoutedEventArgs e)
    {
        var url = TxtIntifaceUrl.Text.Trim();
        if (string.IsNullOrEmpty(url)) return;

        BtnConnect.IsEnabled = false;
        TxtConnectionStatus.Text = "Connecting...";
        var connectingBrush = (System.Windows.Media.Brush)Application.Current.Resources["StatusIdleBrush"];
        EllipseStatus.Fill = connectingBrush;

        try
        {
            await _intifaceService.ConnectAsync(url);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to connect to Intiface Central:\n{ex.Message}", "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
            TxtConnectionStatus.Text = "Disconnected";
            EllipseStatus.Fill = (System.Windows.Media.Brush)Application.Current.Resources["StatusDisconnectedBrush"];
            BtnConnect.IsEnabled = true;
        }
    }

    private async void BtnDisconnect_Click(object sender, RoutedEventArgs e)
    {
        BtnDisconnect.IsEnabled = false;
        await _intifaceService.DisconnectAsync();
        CmbDevices.Items.Clear();
    }

    private async void BtnTestDevice_Click(object sender, RoutedEventArgs e)
    {
        if (CmbDevices.SelectedItem is not string deviceName || string.IsNullOrEmpty(deviceName))
            return;

        BtnTestDevice.IsEnabled = false;
        try
        {
            // Send a test pulse: 50% intensity for 500ms
            await _intifaceService.SendVibratePulseAsync(deviceName, 0.5, 500);
        }
        finally
        {
            if (_intifaceService.IsConnected && CmbDevices.SelectedItem != null)
            {
                BtnTestDevice.IsEnabled = true;
            }
        }
    }

    // ─────────────────────────────────────────────
    // Log File Panel
    // ─────────────────────────────────────────────

    private void BtnBrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select CtrlEm Log Folder",
            InitialDirectory = Directory.Exists(_config.LogFolderPath)
                ? _config.LogFolderPath
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };

        if (dialog.ShowDialog(this) != true) return;

        var selected = dialog.FolderName;
        _config.LogFolderPath = selected;
        TxtLogFolder.Text = selected;
        ConfigService.Save(_config);
        StartLogMonitoring();
    }

    // ─────────────────────────────────────────────
    // Bottom Controls
    // ─────────────────────────────────────────────

    private void BtnStart_Click(object sender, RoutedEventArgs e) => StartRunning();

    private void StartRunning()
    {
        if (!_intifaceService.IsConnected || CmbDevices.SelectedItem is not string deviceName || string.IsNullOrEmpty(deviceName))
            return;

        _isRunning = true;
        TxtLinesMatched.Text = "0";
        CmbDevices.IsEnabled = false;
        BtnTestDevice.IsEnabled = false;
        BtnStart.IsEnabled = false;
        BtnStop.IsEnabled = true;
        _trayMenuStart.Enabled = false;
        _trayMenuStop.Enabled = true;

        EllipseRunStatus.Fill = (System.Windows.Media.Brush)Application.Current.Resources["StatusConnectedBrush"];
        TxtStatusMessage.Text = $"Running — monitoring log and sending commands to {deviceName}";
    }

    private void BtnStop_Click(object sender, RoutedEventArgs e)
    {
        StopRunning();
    }

    private async void StopRunning()
    {
        _isRunning = false;

        // Cancel any in-flight Task.Delay and drain queued commands immediately
        _intifaceService.CancelPulse();

        if (CmbDevices.SelectedItem is string deviceName && !string.IsNullOrEmpty(deviceName))
        {
            // Send stop (intensity 0) directly — bypasses the queue for immediate effect
            await _intifaceService.SendStopAsync(deviceName);
        }

        bool canStart = _intifaceService.IsConnected && CmbDevices.SelectedItem != null;
        CmbDevices.IsEnabled = _intifaceService.IsConnected;
        BtnTestDevice.IsEnabled = canStart;
        BtnStart.IsEnabled = canStart;
        BtnStop.IsEnabled = false;
        _trayMenuStart.Enabled = canStart;
        _trayMenuStop.Enabled = false;

        EllipseRunStatus.Fill = (System.Windows.Media.Brush)Application.Current.Resources["StatusIdleBrush"];
        TxtStatusMessage.Text = _intifaceService.IsConnected ? "Stopped — press Start to resume" : "Idle — connect to Intiface and press Start";
        TxtLastCommand.Text = "— idle —";
    }

    // ─────────────────────────────────────────────
    // Window Lifecycle
    // ─────────────────────────────────────────────

    private void BtnExit_Click(object sender, RoutedEventArgs e) => ExitApplication();

    private async void ExitApplication()
    {
        _isExiting = true;

        // Persist current theme mode to config.json
        _config.DarkMode = _isDarkMode;
        ConfigService.Save(_config);

        // 1. Stop log monitor immediately (cancels its background Task)
        _logMonitor.Dispose();

        // 2. Cancel any in-flight vibration pulse and disconnect within a 2-second hard timeout
        await _intifaceService.ShutdownAsync();
        _intifaceService.Dispose();

        // 3. Remove tray icon and shut down
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        Application.Current.Shutdown();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_isExiting)
        {
            e.Cancel = true;
            Hide();

            // Show the system tray notification only the first time the window is minimized
            if (!_trayBalloonShown)
            {
                _trayBalloonShown = true;
                _trayIcon.ShowBalloonTip(
                    3000,
                    "Vibing With CtrlEm is still running",
                    "The application has been minimized to the system tray. Right-click or double-click the tray icon to restore or exit.",
                    ToolTipIcon.Info);
            }
            return;
        }

        base.OnClosing(e);
    }
}