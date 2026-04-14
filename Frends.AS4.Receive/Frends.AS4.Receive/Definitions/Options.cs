using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Frends.AS4.Receive.Definitions;

/// <summary>
/// Additional options controlling the AS4 Receive task behaviour.
/// </summary>
public class Options
{
    /// <summary>
    /// When true, the WS-Security digital signature on the inbound message is verified
    /// using RSA-SHA256. The signature covers the eb:Messaging header and the payload attachment.
    /// An exception is thrown if verification fails.
    /// </summary>
    /// <example>true</example>
    [Display(Name = "Verify Signature")]
    [DefaultValue(true)]
    public bool VerifySignature { get; set; } = true;

    /// <summary>
    /// When true, the task attempts to decrypt the payload attachment using the receiver
    /// certificate. The AES-256 session key is unwrapped from the xenc:EncryptedKey element
    /// in the WS-Security header using RSA-OAEP.
    /// </summary>
    /// <example>true</example>
    [Display(Name = "Decrypt Payload")]
    [DefaultValue(true)]
    public bool DecryptPayload { get; set; } = true;

    /// <summary>
    /// When true, the task decompresses the payload if GZip or Deflate compression is
    /// detected (either via eb:PartProperties or by inspecting the magic bytes).
    /// </summary>
    /// <example>true</example>
    [Display(Name = "Decompress Payload")]
    [DefaultValue(true)]
    public bool DecompressPayload { get; set; } = true;

    /// <summary>
    /// When true, any exception during task execution is re-thrown and propagates to the
    /// Frends process. When false, the exception is captured in Result.Error and
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
    /// <example>AS4 receive failed</example>
    [DisplayFormat(DataFormatString = "Text")]
    [DefaultValue("")]
    public string ErrorMessageOnFailure { get; set; } = string.Empty;
}
