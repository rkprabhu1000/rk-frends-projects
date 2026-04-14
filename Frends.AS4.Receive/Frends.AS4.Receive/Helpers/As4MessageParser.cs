using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;
using Frends.AS4.Receive.Definitions;
using MimeKit;

namespace Frends.AS4.Receive.Helpers;

/// <summary>
/// Parses an inbound AS4 Multipart/Related HTTP request body into its constituent
/// SOAP envelope and payload attachment, with optional decryption and decompression.
/// </summary>
internal static class As4MessageParser
{
    private const string NsEbms = "http://docs.oasis-open.org/ebxml-msg/ebms/v3.0/ns/core/20070523/";
    private const string NsXenc = "http://www.w3.org/2001/04/xmlenc#";
    private const string NsWsse = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";
    private const string NsDs = "http://www.w3.org/2000/09/xmldsig#";

    /// <summary>
    /// Parses the raw request body, extracts the SOAP envelope and attachment,
    /// and applies optional decryption and decompression.
    /// </summary>
    internal static ParsedAs4Message Parse(
        string requestBody,
        string contentTypeHeader,
        X509Certificate2 receiverCert,
        bool decrypt,
        bool decompress)
    {
        var (soapDoc, attachmentBytes, attachmentCid) = ExtractMimeParts(requestBody, contentTypeHeader);

        // Extract routing metadata from eb:Messaging
        var metadata = ExtractMessagingMetadata(soapDoc);

        // Decrypt if required and EncryptedKey is present in the Security header
        if (decrypt && receiverCert != null && HasEncryptedKey(soapDoc))
            attachmentBytes = DecryptAttachment(soapDoc, attachmentBytes, receiverCert);

        // Decompress if required
        if (decompress && attachmentBytes != null)
            attachmentBytes = TryDecompress(attachmentBytes);

        return new ParsedAs4Message
        {
            SoapDocument = soapDoc,
            AttachmentBytes = attachmentBytes,
            AttachmentCid = attachmentCid,
            Metadata = metadata,
        };
    }

    // -------------------------------------------------------------------------
    // MIME parsing
    // -------------------------------------------------------------------------

    private static (XmlDocument soapDoc, byte[] attachmentBytes, string attachmentCid) ExtractMimeParts(
        string requestBody, string contentTypeHeader)
    {
        // Reconstruct a parseable MIME entity by prepending the Content-Type header
        var raw = Encoding.UTF8.GetBytes($"Content-Type: {contentTypeHeader}\r\n\r\n{requestBody}");

        MimeEntity entity;
        using (var ms = new MemoryStream(raw))
            entity = MimeEntity.Load(ms);

        if (entity is not Multipart multipart)
            throw new FormatException("The request body is not a Multipart/Related MIME message.");

        // First part must be the SOAP envelope
        var soapPart = multipart.OfType<MimePart>().FirstOrDefault(p =>
            p.ContentType.MimeType.Contains("soap", StringComparison.OrdinalIgnoreCase) ||
            p.ContentType.MimeType.Contains("xml", StringComparison.OrdinalIgnoreCase));

        if (soapPart == null)
            throw new FormatException("No SOAP part found in the Multipart/Related message.");

        using var soapMs = new MemoryStream();
        soapPart.Content.DecodeTo(soapMs);
        var soapXml = Encoding.UTF8.GetString(soapMs.ToArray());

        var soapDoc = new XmlDocument { PreserveWhitespace = false };
        soapDoc.LoadXml(soapXml);

        // Remaining non-SOAP parts are attachments; take the first
        var attachmentPart = multipart.OfType<MimePart>().FirstOrDefault(p =>
            !p.ContentType.MimeType.Contains("soap", StringComparison.OrdinalIgnoreCase) &&
            !p.ContentType.MimeType.Contains("xml", StringComparison.OrdinalIgnoreCase));

        byte[] attachmentBytes = null;
        string attachmentCid = null;

        if (attachmentPart != null)
        {
            using var attMs = new MemoryStream();
            attachmentPart.Content.DecodeTo(attMs);
            attachmentBytes = attMs.ToArray();
            attachmentCid = attachmentPart.ContentId;
        }

        return (soapDoc, attachmentBytes, attachmentCid);
    }

    // -------------------------------------------------------------------------
    // eb:Messaging metadata extraction
    // -------------------------------------------------------------------------

    internal static As4RoutingMetadata ExtractMessagingMetadata(XmlDocument soapDoc)
    {
        var ns = BuildNsManager(soapDoc);

        var metadata = new As4RoutingMetadata();

        metadata.MessageId = SelectText(soapDoc, "//eb:Messaging/eb:UserMessage/eb:MessageInfo/eb:MessageId", ns);
        metadata.SenderPartyId = SelectText(soapDoc, "//eb:Messaging/eb:UserMessage/eb:PartyInfo/eb:From/eb:PartyId", ns);
        metadata.RecipientPartyId = SelectText(soapDoc, "//eb:Messaging/eb:UserMessage/eb:PartyInfo/eb:To/eb:PartyId", ns);
        metadata.Service = SelectText(soapDoc, "//eb:Messaging/eb:UserMessage/eb:CollaborationInfo/eb:Service", ns);
        metadata.Action = SelectText(soapDoc, "//eb:Messaging/eb:UserMessage/eb:CollaborationInfo/eb:Action", ns);
        metadata.ConversationId = SelectText(soapDoc, "//eb:Messaging/eb:UserMessage/eb:CollaborationInfo/eb:ConversationId", ns);
        metadata.AgreementRef = SelectText(soapDoc, "//eb:Messaging/eb:UserMessage/eb:CollaborationInfo/eb:AgreementRef", ns);

        return metadata;
    }

    // -------------------------------------------------------------------------
    // Decryption
    // -------------------------------------------------------------------------

    private static bool HasEncryptedKey(XmlDocument soapDoc)
    {
        var ns = BuildNsManager(soapDoc);
        return soapDoc.SelectSingleNode("//xenc:EncryptedKey", ns) != null;
    }

    internal static byte[] DecryptAttachment(XmlDocument soapDoc, byte[] cipherBytes, X509Certificate2 receiverCert)
    {
        var ns = BuildNsManager(soapDoc);

        // Extract the RSA-wrapped AES session key from xenc:EncryptedKey/xenc:CipherData/xenc:CipherValue
        var cipherValueNode = soapDoc.SelectSingleNode(
            "//xenc:EncryptedKey/xenc:CipherData/xenc:CipherValue", ns)
            ?? throw new InvalidOperationException("No xenc:CipherValue found in EncryptedKey element.");

        var wrappedKey = Convert.FromBase64String(cipherValueNode.InnerText.Trim());

        // Unwrap using receiver's private RSA key (OAEP-SHA1 per XML Enc spec)
        using var rsa = receiverCert.GetRSAPrivateKey()
            ?? throw new InvalidOperationException("The receiver certificate does not contain a private key.");

        var sessionKey = rsa.Decrypt(wrappedKey, RSAEncryptionPadding.OaepSHA1);

        // The attachment format written by As4MessageBuilder: [16-byte IV][AES-CBC ciphertext]
        if (cipherBytes.Length <= 16)
            throw new InvalidOperationException("Encrypted attachment is too short to contain a valid IV.");

        var iv = cipherBytes[..16];
        var cipherText = cipherBytes[16..];

        return DecryptWithSessionKey(cipherText, iv, sessionKey);
    }

    internal static byte[] DecryptWithSessionKey(byte[] cipherText, byte[] iv, byte[] key)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;

        using var decryptor = aes.CreateDecryptor();
        using var ms = new MemoryStream();
        using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Write))
            cs.Write(cipherText, 0, cipherText.Length);
        return ms.ToArray();
    }

    // -------------------------------------------------------------------------
    // Decompression
    // -------------------------------------------------------------------------

    internal static byte[] TryDecompress(byte[] data)
    {
        if (data == null || data.Length < 2)
            return data;

        // GZip magic: 1F 8B
        if (data[0] == 0x1F && data[1] == 0x8B)
            return DecompressGzip(data);

        // Zlib magic: 78 xx
        if (data[0] == 0x78)
            return DecompressDeflate(data[2..]);   // skip 2-byte zlib header

        return data;
    }

    private static byte[] DecompressGzip(byte[] compressed)
    {
        using var input = new MemoryStream(compressed);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }

    private static byte[] DecompressDeflate(byte[] compressed)
    {
        using var input = new MemoryStream(compressed);
        using var deflate = new DeflateStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        deflate.CopyTo(output);
        return output.ToArray();
    }

    // -------------------------------------------------------------------------
    // Utilities
    // -------------------------------------------------------------------------

    internal static string TryDecodeUtf8(byte[] bytes)
    {
        if (bytes == null) return null;
        try { return Encoding.UTF8.GetString(bytes); }
        catch { return Convert.ToBase64String(bytes); }
    }

    private static XmlNamespaceManager BuildNsManager(XmlDocument doc)
    {
        var ns = new XmlNamespaceManager(doc.NameTable);
        ns.AddNamespace("eb", NsEbms);
        ns.AddNamespace("xenc", NsXenc);
        ns.AddNamespace("wsse", NsWsse);
        ns.AddNamespace("ds", NsDs);
        return ns;
    }

    private static string SelectText(XmlDocument doc, string xpath, XmlNamespaceManager ns)
        => doc.SelectSingleNode(xpath, ns)?.InnerText?.Trim();
}

/// <summary>
/// Holds the parsed components of an inbound AS4 message.
/// </summary>
internal class ParsedAs4Message
{
    internal XmlDocument SoapDocument { get; set; }
    internal byte[] AttachmentBytes { get; set; }
    internal string AttachmentCid { get; set; }
    internal As4RoutingMetadata Metadata { get; set; }
}

/// <summary>
/// Routing metadata extracted from the eb:Messaging SOAP header.
/// </summary>
internal class As4RoutingMetadata
{
    internal string MessageId { get; set; }
    internal string SenderPartyId { get; set; }
    internal string RecipientPartyId { get; set; }
    internal string Service { get; set; }
    internal string Action { get; set; }
    internal string ConversationId { get; set; }
    internal string AgreementRef { get; set; }
}
