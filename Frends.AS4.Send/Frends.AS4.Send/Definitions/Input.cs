using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Frends.AS4.Send.Definitions;

/// <summary>
/// Input parameters for the AS4 Send task.
/// </summary>
public class Input
{
    /// <summary>
    /// Full path to the file to be sent as the AS4 payload attachment.
    /// </summary>
    /// <example>C:\payloads\invoice.xml</example>
    [DisplayFormat(DataFormatString = "Text")]
    [DefaultValue("")]
    public string FilePath { get; set; }

    /// <summary>
    /// The HTTP(S) endpoint URL of the AS4 receiving MSH.
    /// </summary>
    /// <example>https://partner.example.com/as4/receive</example>
    [DisplayFormat(DataFormatString = "Text")]
    [DefaultValue("")]
    public string RecipientUrl { get; set; }

    /// <summary>
    /// AS4 party identifier for the sender (From party).
    /// </summary>
    /// <example>urn:party:sender:acme</example>
    [Display(Name = "Sender Party ID")]
    [DisplayFormat(DataFormatString = "Text")]
    [DefaultValue("")]
    public string SenderPartyId { get; set; }

    /// <summary>
    /// AS4 party identifier for the recipient (To party).
    /// </summary>
    /// <example>urn:party:recipient:partner</example>
    [Display(Name = "Recipient Party ID")]
    [DisplayFormat(DataFormatString = "Text")]
    [DefaultValue("")]
    public string RecipientPartyId { get; set; }

    /// <summary>
    /// AS4 collaboration service value included in the eb:CollaborationInfo header.
    /// </summary>
    /// <example>urn:services:PackageService</example>
    [DisplayFormat(DataFormatString = "Text")]
    [DefaultValue("")]
    public string Service { get; set; }

    /// <summary>
    /// AS4 action value included in the eb:CollaborationInfo header.
    /// </summary>
    /// <example>Deliver</example>
    [DisplayFormat(DataFormatString = "Text")]
    [DefaultValue("")]
    public string Action { get; set; }

    /// <summary>
    /// Unique identifier for the AS4 conversation. Must be consistent across all messages in the same exchange.
    /// </summary>
    /// <example>conv-2026-04-10-001</example>
    [Display(Name = "Conversation ID")]
    [DisplayFormat(DataFormatString = "Text")]
    [DefaultValue("")]
    public string ConversationId { get; set; }

    /// <summary>
    /// Optional reference to the P-Mode agreement governing this message exchange.
    /// Leave empty if not required by the receiving MSH.
    /// </summary>
    /// <example>urn:agreements:PMode-Deliver-v1</example>
    [Display(Name = "Agreement Reference")]
    [DisplayFormat(DataFormatString = "Text")]
    [DefaultValue("")]
    public string AgreementRef { get; set; }

    /// <summary>
    /// File system path to the PFX (PKCS#12) certificate used to sign the outgoing message.
    /// </summary>
    /// <example>C:\certs\sender.pfx</example>
    [Display(Name = "Sender Certificate Path")]
    [DisplayFormat(DataFormatString = "Text")]
    [DefaultValue("")]
    public string SenderCertificatePath { get; set; }

    /// <summary>
    /// Password protecting the sender PFX certificate file.
    /// </summary>
    /// <example>P@ssw0rd!</example>
    [Display(Name = "Sender Certificate Password")]
    [PasswordPropertyText(true)]
    [DefaultValue("")]
    public string SenderCertificatePassword { get; set; }

    /// <summary>
    /// File system path to the recipient's public X.509 certificate (.cer/.crt) used to encrypt the outgoing payload.
    /// </summary>
    /// <example>C:\certs\recipient-public.cer</example>
    [Display(Name = "Recipient Certificate Path")]
    [DisplayFormat(DataFormatString = "Text")]
    [DefaultValue("")]
    public string RecipientCertificatePath { get; set; }

    /// <summary>
    /// Optional path to a PFX certificate used to decrypt the response payload.
    /// Defaults to the sender certificate when left empty.
    /// </summary>
    /// <example>C:\certs\sender.pfx</example>
    [Display(Name = "Response Decryption Certificate Path")]
    [DisplayFormat(DataFormatString = "Text")]
    [DefaultValue("")]
    public string ResponseDecryptionCertificatePath { get; set; }

    /// <summary>
    /// Password for the response decryption certificate.
    /// Required only when a separate response decryption certificate path is provided.
    /// </summary>
    /// <example>P@ssw0rd!</example>
    [Display(Name = "Response Decryption Certificate Password")]
    [PasswordPropertyText(true)]
    [DefaultValue("")]
    public string ResponseDecryptionCertificatePassword { get; set; }
}
