using System;

namespace Frends.AS4.Receive.Definitions;

/// <summary>
/// Error information returned when the AS4 Receive task encounters a failure.
/// </summary>
public class Error
{
    /// <summary>
    /// Human-readable summary of the error.
    /// </summary>
    /// <example>Signature verification failed: digest mismatch on eb:Messaging.</example>
    public string Message { get; set; }

    /// <summary>
    /// The underlying exception providing stack trace and inner exception details.
    /// </summary>
    /// <example>System.Security.Cryptography.CryptographicException</example>
    public Exception AdditionalInfo { get; set; }
}
