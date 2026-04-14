using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Frends.AS4.Receive.Definitions;

/// <summary>
/// Input parameters for the AS4 Receive task.
/// </summary>
public class Input
{
    /// <summary>
    /// The raw HTTP request body received from the Frends HTTP trigger.
    /// This is the full Multipart/Related MIME body containing the SOAP envelope
    /// and any payload attachments.
    /// </summary>
    /// <example>#trigger.body</example>
    [DisplayFormat(DataFormatString = "Text")]
    [DefaultValue("")]
    public string RequestBody { get; set; }

    /// <summary>
    /// The value of the Content-Type HTTP request header, including the multipart boundary.
    /// Typically sourced from the Frends HTTP trigger's header collection.
    /// </summary>
    /// <example>multipart/related; type="application/soap+xml"; boundary="MIMEBoundary"</example>
    [Display(Name = "Content-Type Header")]
    [DisplayFormat(DataFormatString = "Text")]
    [DefaultValue("")]
    public string ContentTypeHeader { get; set; }

    /// <summary>
    /// File system path to the receiver's PFX (PKCS#12) certificate used to decrypt
    /// the inbound payload. Must contain the private key.
    /// </summary>
    /// <example>C:\certs\receiver.pfx</example>
    [Display(Name = "Receiver Certificate Path")]
    [DisplayFormat(DataFormatString = "Text")]
    [DefaultValue("")]
    public string ReceiverCertificatePath { get; set; }

    /// <summary>
    /// Password protecting the receiver PFX certificate file.
    /// </summary>
    /// <example>P@ssw0rd!</example>
    [Display(Name = "Receiver Certificate Password")]
    [PasswordPropertyText(true)]
    [DefaultValue("")]
    public string ReceiverCertificatePassword { get; set; }

    /// <summary>
    /// Optional file system path to the sender's public X.509 certificate (.cer/.crt)
    /// used to verify the WS-Security signature on the inbound message.
    /// When left empty and VerifySignature is true, the certificate embedded in the
    /// SOAP wsse:BinarySecurityToken is trusted directly (no external trust anchor check).
    /// </summary>
    /// <example>C:\certs\sender-public.cer</example>
    [Display(Name = "Sender Certificate Path (for signature verification)")]
    [DisplayFormat(DataFormatString = "Text")]
    [DefaultValue("")]
    public string SenderCertificatePath { get; set; }
}
