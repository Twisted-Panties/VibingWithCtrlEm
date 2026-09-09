using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VibingWithCtrlEm.Models;
using VibingWithCtrlEm.Services;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;

namespace VibingWithCtrlEm.Views;

/// <summary>
/// Interaction logic for FeedbackWindow.xaml
/// </summary>
public partial class FeedbackWindow : Window
{
    private CancellationTokenSource? _sendCts;

    public FeedbackWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => TxtFeedback.Focus();
    }

    private async void BtnSend_Click(object sender, RoutedEventArgs e)
    {
        var feedbackText = TxtFeedback.Text.Trim();
        if (string.IsNullOrWhiteSpace(feedbackText))
        {
            SetStatus("Please enter your feedback before sending.", isError: true);
            TxtFeedback.Focus();
            return;
        }

        var category = FeedbackCategory.Suggestion;
        if (CmbCategory.SelectedItem is ComboBoxItem selectedItem &&
            selectedItem.Tag is string tag &&
            Enum.TryParse<FeedbackCategory>(tag, out var parsedCategory))
        {
            category = parsedCategory;
        }

        var submission = new FeedbackSubmission
        {
            Category = category,
            Message = feedbackText,
            Contact = string.IsNullOrWhiteSpace(TxtContact.Text) ? null : TxtContact.Text.Trim(),
            IncludeSystemInfo = ChkIncludeSystemInfo.IsChecked == true
        };

        SetInputsEnabled(false);
        ProgressBarSending.Visibility = Visibility.Visible;
        SetStatus("Sending feedback...", isError: false);

        _sendCts = new CancellationTokenSource();

        try
        {
            var (success, message) = await FeedbackService.SendFeedbackAsync(submission, _sendCts.Token);

            if (success)
            {
                ProgressBarSending.Visibility = Visibility.Collapsed;
                SetStatus("✓ Feedback sent successfully! Thank you.", isSuccess: true);
                await Task.Delay(1200);
                Close();
            }
            else
            {
                ProgressBarSending.Visibility = Visibility.Collapsed;
                SetStatus(message, isError: true);
                SetInputsEnabled(true);
            }
        }
        catch (Exception ex)
        {
            ProgressBarSending.Visibility = Visibility.Collapsed;
            SetStatus($"Error: {ex.Message}", isError: true);
            SetInputsEnabled(true);
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        _sendCts?.Cancel();
        Close();
    }

    private void SetInputsEnabled(bool isEnabled)
    {
        BtnSend.IsEnabled = isEnabled;
        BtnCancel.IsEnabled = isEnabled;
        TxtFeedback.IsEnabled = isEnabled;
        TxtContact.IsEnabled = isEnabled;
        CmbCategory.IsEnabled = isEnabled;
        ChkIncludeSystemInfo.IsEnabled = isEnabled;
    }

    private void SetStatus(string message, bool isError = false, bool isSuccess = false)
    {
        TxtStatus.Text = message;
        if (isError)
        {
            TxtStatus.Foreground = FindResource("StatusDisconnectedBrush") as Brush
                ?? Brushes.Red;
        }
        else if (isSuccess)
        {
            TxtStatus.Foreground = FindResource("StatusConnectedBrush") as Brush
                ?? Brushes.Green;
        }
        else
        {
            TxtStatus.Foreground = FindResource("SecondaryTextBrush") as Brush
                ?? Brushes.Gray;
        }
    }
}
