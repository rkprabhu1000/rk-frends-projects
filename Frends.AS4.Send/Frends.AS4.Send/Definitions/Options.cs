using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Frends.AS4.Send.Definitions;

/// <summary>
/// Additional options controlling the AS4 Send task behaviour.
/// </summary>
public class Options
{
    /// <summary>
    /// When true, the outgoing SOAP message will be signed using the sender certificate
    /// with RSA-SHA256 and an XML Digital Signature in the WS-Security header.
    /// </summary>
    /// <example>true</example>
    [Display(Name = "Sign Message")]
    [DefaultValue(true)]
    public bool SignMessage { get; set; } = true;

    /// <summary>
    /// When true, the payload attachment will be encrypted using AES-256-CBC
    /// with the session key wrapped by the recipient certificate (RSA-OAEP).
    /// </summary>
    /// <example>true</example>
    [Display(Name = "Encrypt Message")]
    [DefaultValue(true)]
    public bool EncryptMessage { get; set; } = true;

    /// <summary>
    /// When true, the task will attempt to decrypt the response payload
    /// using the response decryption certificate (or the sender certificate if not provided).
    /// </summary>
    /// <example>true</example>
    [Display(Name = "Decrypt Response")]
    [DefaultValue(true)]
    public bool DecryptResponse { get; set; } = true;

    /// <summary>
    /// When true, the task will attempt to decompress the response payload
    /// if the eb:PartProperties indicate GZip or Deflate compression,
    /// or if the content starts with the GZip magic bytes.
    /// </summary>
    /// <example>true</example>
    [Display(Name = "Decompress Response")]
    [DefaultValue(true)]
    public bool DecompressResponse { get; set; } = true;

    /// <summary>
    /// When true, any exception during task execution is re-thrown and propagates to the Frends process.
    /// When false, the exception is captured in the Result.Error property and Result.Success is set to false.
    /// </summary>
    /// <example>true</example>
    [Display(Name = "Throw Error On Failure")]
    [DefaultValue(true)]
    public bool ThrowErrorOnFailure { get; set; } = true;

    /// <summary>
    /// Overrides the error message returned or thrown on failure.
    /// When empty, the original exception message is used.
    /// </summary>
    /// <example>AS4 send failed</example>
    [DisplayFormat(DataFormatString = "Text")]
    [DefaultValue("")]
    public string ErrorMessageOnFailure { get; set; } = string.Empty;
}
