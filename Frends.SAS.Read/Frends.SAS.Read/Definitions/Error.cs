using System;

namespace Frends.SAS.Read.Definitions;

/// <summary>
/// Error information returned when the SAS Read task encounters a failure.
/// </summary>
public class Error
{
    /// <summary>
    /// Human-readable summary of the error.
    /// </summary>
    /// <example>Failed to parse .sas7bdat file header.</example>
    public string Message { get; set; }

    /// <summary>
    /// The underlying exception providing stack trace and inner exception details.
    /// </summary>
    /// <example>System.Collections.Generic.KeyNotFoundException</example>
    public Exception AdditionalInfo { get; set; }
}
