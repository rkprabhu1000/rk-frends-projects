using System.Collections.Generic;

namespace Frends.AS4.Send.Definitions;

/// <summary>
/// Result returned by the AS4 Send task after processing the synchronous response.
/// </summary>
public class Result
{
    /// <summary>
    /// Indicates whether the task completed successfully, including sending the request
    /// and processing the response payload.
    /// </summary>
    /// <example>true</example>
    public bool Success { get; set; }

    /// <summary>
    /// The processed response payload decoded as a UTF-8 string.
    /// Null when the response contains no payload part or when Success is false.
    /// </summary>
    /// <example>&lt;Invoice xmlns="urn:invoice"&gt;...&lt;/Invoice&gt;</example>
    public string PayloadString { get; set; }

    /// <summary>
    /// The raw bytes of the processed response payload after any decryption and decompression.
    /// Null when the response contains no payload part or when Success is false.
    /// </summary>
    /// <example>[byte array]</example>
    public byte[] PayloadBytes { get; set; }

    /// <summary>
    /// HTTP response headers returned by the receiving MSH, keyed by header name.
    /// </summary>
    /// <example>object { "Content-Type": "multipart/related; ...", "X-AS4-MessageId": "..." }</example>
    public Dictionary<string, string> ResponseHeaders { get; set; }

    /// <summary>
    /// Error information populated when Success is false.
    /// Null on successful completion.
    /// </summary>
    /// <example>object { string Message, Exception AdditionalInfo }</example>
    public Error Error { get; set; }
}
