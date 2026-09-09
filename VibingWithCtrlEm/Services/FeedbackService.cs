using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using VibingWithCtrlEm.Models;

namespace VibingWithCtrlEm.Services;

/// <summary>
/// Service responsible for packaging user feedback into a Discord-compatible embed
/// and transmitting it to a Cloudflare Worker relay.
/// </summary>
public static class FeedbackService
{
    /// <summary>
    /// Deployed Cloudflare Worker relay URL.
    /// </summary>
    public const string FeedbackEndpointUrl = "https://vibing-feedback.sirrounded-tp.workers.dev";

    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    /// <summary>
    /// Packages and transmits feedback asynchronously.
    /// </summary>
    /// <param name="submission">Feedback content and options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple indicating success status and an informational message.</returns>
    public static async Task<(bool Success, string Message)> SendFeedbackAsync(
        FeedbackSubmission submission,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(submission.Message))
        {
            return (false, "Please enter your feedback before sending.");
        }

        try
        {
            var payload = BuildDiscordPayload(submission);
            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await HttpClient.PostAsync(FeedbackEndpointUrl, content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return (true, "Feedback sent successfully! Thank you for taking the time to share your thoughts.");
            }

            var statusCode = (int)response.StatusCode;
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (statusCode == 429)
            {
                return (false, "You are submitting feedback too quickly. Please wait a minute before trying again.");
            }

            return (false, $"Server returned HTTP {statusCode} ({response.ReasonPhrase}). Details: {responseBody}");
        }
        catch (TaskCanceledException)
        {
            return (false, "Request timed out. Please check your internet connection and try again.");
        }
        catch (HttpRequestException ex)
        {
            return (false, $"Network error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return (false, $"An unexpected error occurred: {ex.Message}");
        }
    }

    /// <summary>
    /// Constructs a Discord Webhook compatible payload with an embed.
    /// </summary>
    private static object BuildDiscordPayload(FeedbackSubmission submission)
    {
        var (categoryTitle, embedColor) = GetCategoryDetails(submission.Category);

        var fields = new List<object>
        {
            new
            {
                name = "👤 Contact",
                value = string.IsNullOrWhiteSpace(submission.Contact) ? "*(Anonymous)*" : submission.Contact.Trim(),
                @inline = true
            }
        };

        if (submission.IncludeSystemInfo)
        {
            var version = UpdateService.CurrentVersion;
            var versionString = $"v{version.Major}.{version.Minor}.{version.Build}";

            fields.Add(new
            {
                name = "🏷️ App Version",
                value = versionString,
                @inline = true
            });

            fields.Add(new
            {
                name = "💻 OS / Environment",
                value = $"{RuntimeInformation.OSDescription} ({RuntimeInformation.ProcessArchitecture})",
                @inline = false
            });
        }

        // Discord embed description limit is 4096 characters
        var description = submission.Message.Trim();
        if (description.Length > 4000)
        {
            description = string.Concat(description.AsSpan(0, 3990), "\n... [truncated]");
        }

        var embed = new
        {
            title = categoryTitle,
            description,
            color = embedColor,
            fields,
            footer = new
            {
                text = "Vibing With CtrlEm • In-App Feedback"
            },
            timestamp = DateTime.UtcNow.ToString("o")
        };

        return new
        {
            username = "Vibing With CtrlEm Feedback",
            avatar_url = "https://raw.githubusercontent.com/Twisted-Panties/VibingWithCtrlEm/main/VibingWithCtrlEm/Assets/app-icon.png",
            embeds = new[] { embed }
        };
    }

    /// <summary>
    /// Returns human-readable title and hex color integer for each feedback category.
    /// </summary>
    public static (string Title, int Color) GetCategoryDetails(FeedbackCategory category) => category switch
    {
        FeedbackCategory.BugReport   => ("🐛 Bug / Error Report", 0xE94560),      // Coral Red
        FeedbackCategory.Complaint   => ("⚠️ Complaint / Issue", 0xFFA726),        // Orange
        FeedbackCategory.Praise      => ("❤️ Positive Feedback / Praise", 0x66BB6A),// Green
        FeedbackCategory.General     => ("💬 General Feedback", 0x7E57C2),         // Purple
        _                            => ("💡 Suggestion / Feature Request", 0x4FC3F7) // Light Cyan
    };
}
