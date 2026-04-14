using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Frends.AS4.PullRequest.Definitions;

/// <summary>
/// Input parameters for the AS4 HandlePullRequest task.
/// </summary>
public class Input
{
    /// <summary>
    /// The raw HTTP request body received from the Frends HTTP trigger.
    /// Must contain a Multipart/Related SOAP envelope with an eb:PullRequest signal.
    /// </summary>
    /// <example>#trigger.body</example>
    [DisplayFormat(DataFormatString = "Text")]
    [DefaultValue("")]
    public string RequestBody { get; set; }

    /// <summary>
    /// The value of the Content-Type HTTP request header from the Frends HTTP trigger,
    /// including the multipart boundary parameter.
    /// </summary>
    /// <example>multipart/related; type="application/soap+xml"; boundary="MIMEBoundary"</example>
    [Display(Name = "Content-Type Header")]
    [DisplayFormat(DataFormatString = "Text")]
    [DefaultValue("")]
    public string ContentTypeHeader { get; set; }

    /// <summary>
    /// Full path to the file to dequeue and return as the UserMessage payload.
    /// When left empty, the task returns an EBMS:0006 EmptyMessagePartitionChannel
    /// error signal indicating no message is available on the requested MPC.
    /// </summary>
    /// <example>C:\queue\invoice-001.xml</example>
    [Display(Name = "Payload File Path")]
    [DisplayFormat(DataFormatString = "Text")]
    [DefaultValue("")]
    public string PayloadFilePath { get; set; }

    /// <summary>
    /// AS4 party identifier for this system (the party serving the MPC queue).
    /// Included in the eb:From element of the response UserMessage.
    /// </summary>
    /// <example>urn:party:sender:acme</example>
    [Display(Name = "Sender Party ID")]
    [DisplayFormat(DataFormatString = "Text")]
    [DefaultValue("")]
    public string SenderPartyId { get; set; }

    /// <summary>
    /// AS4 party identifier for the pulling partner.
    /// Included in the eb:To element of the response UserMessage.
    /// </summary>
    /// <example>urn:party:recipient:partner</example>
    [Display(Name = "Recipient Party ID")]
    [DisplayFormat(DataFormatString = "Text")]
    [DefaultValue("")]
    public string RecipientPartyId { get; set; }

    /// <summary>
    /// AS4 Service value included in the eb:CollaborationInfo of the response UserMessage.
    /// </summary>
    /// <example>urn:services:CustomsService</example>
    [DisplayFormat(DataFormatString = "Text")]
    [DefaultValue("")]
    public string Service { get; set; }

    /// <summary>
    /// AS4 Action value included in the eb:CollaborationInfo of the response UserMessage.
    /// </summary>
    /// <example>SubmitDeclaration</example>
    [DisplayFormat(DataFormatString = "Text")]
    [DefaultValue("")]
    public string Action { get; set; }

    /// <summary>
    /// Conversation identifier included in the eb:CollaborationInfo of the response UserMessage.
    /// </summary>
    /// <example>conv-2026-04-13-001</example>
    [Display(Name = "Conversation ID")]
    [DisplayFormat(DataFormatString = "Text")]
    [DefaultValue("")]
    public string ConversationId { get; set; }

    /// <summary>
    /// Optional P-Mode agreement reference included in the eb:CollaborationInfo of the response.
    /// Leave empty if not required by the pulling partner.
    /// </summary>
    /// <example>urn:agreements:PMode-Pull-v1</example>
    [Display(Name = "Agreement Reference")]
    [DisplayFormat(DataFormatString = "Text")]
    [DefaultValue("")]
    public string AgreementRef { get; set; }

    /// <summary>
    /// File system path to the PFX (PKCS#12) certificate used to sign the response UserMessage.
    /// Must contain the private key.
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
    /// File system path to the pulling partner's public X.509 certificate (.cer/.crt)
    /// used to encrypt the response payload. The partner uses their private key to decrypt it.
    /// </summary>
    /// <example>C:\certs\partner-public.cer</example>
    [Display(Name = "Recipient Certificate Path")]
    [DisplayFormat(DataFormatString = "Text")]
    [DefaultValue("")]
    public string RecipientCertificatePath { get; set; }
}
