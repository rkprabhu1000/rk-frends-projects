using System;

namespace Frends.AS4.PullRequest.Definitions;

/// <summary>
/// Error information returned when the AS4 HandlePullRequest task encounters a failure.
/// </summary>
public class Error
{
    /// <summary>
    /// Human-readable summary of the error.
    /// </summary>
    /// <example>Failed to parse PullRequest: no eb:PullRequest element found.</example>
    public string Message { get; set; }

    /// <summary>
    /// The underlying exception providing stack trace and inner exception details.
    /// </summary>
    /// <example>System.FormatException</example>
    public Exception AdditionalInfo { get; set; }
}
