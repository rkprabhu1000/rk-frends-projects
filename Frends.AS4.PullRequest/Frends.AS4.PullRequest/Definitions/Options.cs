using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Frends.AS4.PullRequest.Definitions;

/// <summary>
/// Additional options controlling the AS4 HandlePullRequest task behaviour.
/// </summary>
public class Options
{
    /// <summary>
    /// When true, the response UserMessage will be signed using the sender certificate
    /// with RSA-SHA256. Signing covers the eb:Messaging header and the payload attachment.
    /// Per the AS4 profile, response messages MUST be signed when a payload is present.
    /// </summary>
    /// <example>true</example>
    [Display(Name = "Sign Response")]
    [DefaultValue(true)]
    public bool SignResponse { get; set; } = true;

    /// <summary>
    /// When true, the payload attachment will be encrypted using AES-256-CBC with the
    /// session key wrapped using the recipient's public certificate (RSA-OAEP).
    /// </summary>
    /// <example>true</example>
    [Display(Name = "Encrypt Response")]
    [DefaultValue(true)]
    public bool EncryptResponse { get; set; } = true;

    /// <summary>
    /// When true, any exception during task execution is re-thrown and propagates to
    /// the Frends process. When false, the exception is captured in Result.Error and
    /// Result.Success is set to false.
    /// </summary>
    /// <example>true</example>
    [Display(Name = "Throw Error On Failure")]
    [DefaultValue(true)]
    public bool ThrowErrorOnFailure { get; set; } = true;

    /// <summary>
    /// Overrides the error message returned or thrown on failure.
    /// When empty, the original exception message is used.
    /// </summary>
    /// <example>AS4 pull request handling failed</example>
    [DisplayFormat(DataFormatString = "Text")]
    [DefaultValue("")]
    public string ErrorMessageOnFailure { get; set; } = string.Empty;
}
