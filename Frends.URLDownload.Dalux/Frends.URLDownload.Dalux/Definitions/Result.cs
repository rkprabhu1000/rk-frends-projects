namespace Frends.URLDownload.Dalux.Definitions;

/// <summary>
/// Result returned by the Dalux Download task.
/// </summary>
public class Result
{
    /// <summary>
    /// Indicates whether the task completed successfully.
    /// </summary>
    /// <example>true</example>
    public bool Success { get; set; }

    /// <summary>
    /// The filename parsed from the Content-Disposition response header (RFC 5987 UTF-8 encoding).
    /// Falls back to "downloaded_file.pdf" when the header is absent or unparseable.
    /// </summary>
    /// <example>FloorPlan_BuildingA_Rev3.pdf</example>
    public string FileName { get; set; }

    /// <summary>
    /// The raw file bytes returned by the Dalux download endpoint.
    /// Pipe into a Files.Write task to persist to disk, or process in-memory as needed.
    /// </summary>
    /// <example>byte[] file content</example>
    public byte[] Content { get; set; }

    /// <summary>
    /// Error information populated when Success is false. Null on successful completion.
    /// </summary>
    /// <example>object { string Message, Exception AdditionalInfo }</example>
    public Error Error { get; set; }
}
