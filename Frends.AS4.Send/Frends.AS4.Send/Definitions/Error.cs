using System;

namespace Frends.AS4.Send.Definitions;

/// <summary>
/// Error information returned when the AS4 Send task encounters a failure.
/// </summary>
public class Error
{
    /// <summary>
    /// Human-readable summary of the error.
    /// </summary>
    /// <example>Failed to send AS4 message: Connection refused.</example>
    public string Message { get; set; }

    /// <summary>
    /// The underlying exception providing stack trace and inner exception details.
    /// </summary>
    /// <example>System.Net.Http.HttpRequestException: Connection refused.</example>
    public Exception AdditionalInfo { get; set; }
}
