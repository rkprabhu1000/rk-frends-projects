namespace Frends.AS4.PullRequest.Definitions;

/// <summary>
/// Result returned by the AS4 HandlePullRequest task.
/// The ResponseBody and ResponseContentType should be used to populate the
/// Frends HTTP trigger's synchronous response back to the pulling partner.
/// </summary>
public class Result
{
    /// <summary>
    /// Indicates whether the task completed successfully.
    /// </summary>
    /// <example>true</example>
    public bool Success { get; set; }

    /// <summary>
    /// The complete MIME multipart body to return as the HTTP response body.
    /// Contains either a signed/encrypted UserMessage, or an EBMS:0006 error signal.
    /// Set this as the body of the Frends HTTP trigger response.
    /// </summary>
    /// <example>--MIMEBoundary\r\nContent-Type: application/soap+xml...</example>
    public string ResponseBody { get; set; }

    /// <summary>
    /// The Content-Type header value to set on the HTTP response.
    /// Includes the multipart boundary required to parse the MIME body.
    /// </summary>
    /// <example>multipart/related; type="application/soap+xml"; boundary="MIMEBoundary_abc123"</example>
    public string ResponseContentType { get; set; }

    /// <summary>
    /// True when no payload was available and an EBMS:0006
    /// EmptyMessagePartitionChannel error signal was returned instead of a UserMessage.
    /// The calling Frends process can use this to decide whether to mark the queued
    /// item as delivered or to retry later.
    /// </summary>
    /// <example>false</example>
    public bool IsEmptyQueue { get; set; }

    /// <summary>
    /// The Message Partition Channel (MPC) URI extracted from the inbound PullRequest.
    /// Use this to determine which business queue to dequeue from before calling this task.
    /// </summary>
    /// <example>urn:mpc:customs:declarations</example>
    public string RequestedMpc { get; set; }

    /// <summary>
    /// The MessageId extracted from the inbound eb:PullRequest signal.
    /// </summary>
    /// <example>uuid-pull-001@partner.example.com</example>
    public string PullRequestMessageId { get; set; }

    /// <summary>
    /// Error information populated when Success is false. Null on successful completion.
    /// </summary>
    /// <example>object { string Message, Exception AdditionalInfo }</example>
    public Error Error { get; set; }
}
