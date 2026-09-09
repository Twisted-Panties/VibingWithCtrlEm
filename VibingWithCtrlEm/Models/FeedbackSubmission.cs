namespace VibingWithCtrlEm.Models;

/// <summary>
/// Category classification for user-submitted feedback.
/// </summary>
public enum FeedbackCategory
{
    Suggestion,
    BugReport,
    Complaint,
    Praise,
    General
}

/// <summary>
/// Represents the user's feedback input and associated metadata.
/// </summary>
public class FeedbackSubmission
{
    /// <summary>
    /// The user-selected feedback category.
    /// </summary>
    public FeedbackCategory Category { get; set; } = FeedbackCategory.Suggestion;

    /// <summary>
    /// The primary feedback text entered by the user.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Optional Discord handle or email address for follow-up.
    /// </summary>
    public string? Contact { get; set; }

    /// <summary>
    /// Whether to include basic system and application diagnostic info.
    /// </summary>
    public bool IncludeSystemInfo { get; set; } = true;
}
