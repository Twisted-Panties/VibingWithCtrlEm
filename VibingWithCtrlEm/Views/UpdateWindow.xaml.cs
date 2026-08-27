using System.Windows;
using VibingWithCtrlEm.Models;
using VibingWithCtrlEm.Services;

namespace VibingWithCtrlEm.Views;

/// <summary>
/// Dialog window prompting the user to download and apply an available update.
/// </summary>
public partial class UpdateWindow : Window
{
    private readonly UpdateInfo _updateInfo;
    private CancellationTokenSource? _downloadCts;

    public UpdateWindow(UpdateInfo updateInfo)
    {
        InitializeComponent();
        _updateInfo = updateInfo;

        TxtReleaseTitle.Text = string.IsNullOrWhiteSpace(updateInfo.ReleaseName) ? updateInfo.TagName : updateInfo.ReleaseName;
        TxtVersionBadge.Text = updateInfo.TagName;
        TxtCurrentVersion.Text = $"v{UpdateService.CurrentVersion.Major}.{UpdateService.CurrentVersion.Minor}.{UpdateService.CurrentVersion.Build}";
        TxtNewVersion.Text = updateInfo.TagName.StartsWith('v') || updateInfo.TagName.StartsWith('V')
            ? updateInfo.TagName
            : $"v{updateInfo.TagName}";

        TxtChangelog.Text = string.IsNullOrWhiteSpace(updateInfo.Changelog)
            ? "No release notes provided for this version."
            : updateInfo.Changelog.Trim();
    }

    private async void BtnUpdate_Click(object sender, RoutedEventArgs e)
    {
        BtnUpdate.IsEnabled = false;
        BtnLater.IsEnabled = false;
        TxtError.Text = string.Empty;
        PanelProgress.Visibility = Visibility.Visible;
        ProgressBarDownload.Value = 0;
        TxtDownloadStatus.Text = "Connecting to GitHub...";

        _downloadCts = new CancellationTokenSource();

        var progress = new Progress<double>(percent =>
        {
            var p = Math.Clamp(percent * 100, 0, 100);
            ProgressBarDownload.Value = p;
            TxtDownloadStatus.Text = $"Downloading update... {p:0}%";
        });

        try
        {
            var downloadedPath = await UpdateService.DownloadUpdateAsync(
                _updateInfo.DownloadUrl,
                progress,
                _downloadCts.Token);

            TxtDownloadStatus.Text = "Applying update and restarting...";
            await Task.Delay(300); // Brief visual pause so user sees completion

            UpdateService.ApplyUpdateAndRestart(downloadedPath);
        }
        catch (Exception ex)
        {
            PanelProgress.Visibility = Visibility.Collapsed;
            TxtError.Text = $"Update failed: {ex.Message}";
            BtnUpdate.IsEnabled = true;
            BtnLater.IsEnabled = true;
        }
    }

    private void BtnLater_Click(object sender, RoutedEventArgs e)
    {
        _downloadCts?.Cancel();
        Close();
    }
}
