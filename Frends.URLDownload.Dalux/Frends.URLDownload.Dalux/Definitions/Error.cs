using System;

namespace Frends.URLDownload.Dalux.Definitions;

/// <summary>
/// Error information returned when the Dalux Download task encounters a failure.
/// </summary>
public class Error
{
    /// <summary>
    /// Human-readable summary of the error.
    /// </summary>
    /// <example>Failed to capture access token from Dalux Authorize response.</example>
    public string Message { get; set; }

    /// <summary>
    /// The underlying exception providing stack trace and inner exception details.
    /// </summary>
    /// <example>System.InvalidOperationException</example>
    public Exception AdditionalInfo { get; set; }
}
