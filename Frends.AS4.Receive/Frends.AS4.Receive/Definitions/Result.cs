namespace Frends.AS4.Receive.Definitions;

/// <summary>
/// Result returned by the AS4 Receive task after parsing and processing an inbound message.
/// </summary>
public class Result
{
    /// <summary>
    /// Indicates whether the task completed successfully, including MIME parsing,
    /// optional signature verification, decryption, and decompression.
    /// </summary>
    /// <example>true</example>
    public bool Success { get; set; }

    /// <summary>
    /// The processed payload decoded as a UTF-8 string.
    /// Null when the message contains no payload attachment or when Success is false.
    /// </summary>
    /// <example>&lt;Invoice xmlns="urn:invoice"&gt;...&lt;/Invoice&gt;</example>
    public string PayloadString { get; set; }

    /// <summary>
    /// The raw bytes of the processed payload after any decryption and decompression.
    /// Null when the message contains no payload attachment or when Success is false.
    /// </summary>
    /// <example>[byte array]</example>
    public byte[] PayloadBytes { get; set; }

    /// <summary>
    /// The AS4 MessageId extracted from the eb:MessageInfo header.
    /// </summary>
    /// <example>550e8400-e29b-41d4-a716-446655440000@frends.as4</example>
    public string MessageId { get; set; }

    /// <summary>
    /// The sender's AS4 party identifier extracted from the eb:From/eb:PartyId header.
    /// </summary>
    /// <example>urn:party:sender:acme</example>
    public string SenderPartyId { get; set; }

    /// <summary>
    /// The recipient's AS4 party identifier extracted from the eb:To/eb:PartyId header.
    /// </summary>
    /// <example>urn:party:recipient:partner</example>
    public string RecipientPartyId { get; set; }

    /// <summary>
    /// The AS4 Service value extracted from the eb:CollaborationInfo header.
    /// </summary>
    /// <example>urn:services:InvoiceService</example>
    public string Service { get; set; }

    /// <summary>
    /// The AS4 Action value extracted from the eb:CollaborationInfo header.
    /// </summary>
    /// <example>Deliver</example>
    public string Action { get; set; }

    /// <summary>
    /// The conversation identifier extracted from the eb:CollaborationInfo header.
    /// </summary>
    /// <example>conv-2026-04-10-001</example>
    public string ConversationId { get; set; }

    /// <summary>
    /// The P-Mode agreement reference extracted from the eb:CollaborationInfo header.
    /// Null when AgreementRef was not present in the inbound message.
    /// </summary>
    /// <example>urn:agreements:PMode-Deliver-v1</example>
    public string AgreementRef { get; set; }

    /// <summary>
    /// Indicates whether the WS-Security digital signature was present and verified
    /// successfully. False when VerifySignature option was disabled.
    /// </summary>
    /// <example>true</example>
    public bool SignatureVerified { get; set; }

    /// <summary>
    /// Error information populated when Success is false. Null on successful completion.
    /// </summary>
    /// <example>object { string Message, Exception AdditionalInfo }</example>
    public Error Error { get; set; }
}
