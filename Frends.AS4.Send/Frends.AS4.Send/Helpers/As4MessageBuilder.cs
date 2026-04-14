using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml;
using Frends.AS4.Send.Definitions;
using MimeKit;
using MimeKit.Utils;

namespace Frends.AS4.Send.Helpers;

/// <summary>
/// Builds an AS4-compliant Multipart/Related SOAP-with-Attachments message,
/// with optional WS-Security signing and payload encryption.
/// </summary>
internal static class As4MessageBuilder
{
    // XML namespaces used in AS4 / WS-Security
    private const string NsSoap12 = "http://www.w3.org/2003/05/soap-envelope";
    private const string NsEbms = "http://docs.oasis-open.org/ebxml-msg/ebms/v3.0/ns/core/20070523/";
    private const string NsWsse = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";
    private const string NsWsu = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd";
    private const string NsDs = "http://www.w3.org/2000/09/xmldsig#";
    private const string NsXenc = "http://www.w3.org/2001/04/xmlenc#";
    private const string BstValueType = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-x509-token-profile-1.0#X509v3";
    private const string BstEncodingType = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-soap-message-security-1.0#Base64Binary";
    private const string EbDefaultRole = "http://docs.oasis-open.org/ebxml-msg/ebms/v3.0/ns/core/20070523/defaultRole";

    /// <summary>
    /// Builds the complete MimeMessage ready to be serialised as the HTTP request body.
    /// </summary>
    internal static MimeMessage Build(
        byte[] payloadBytes,
        string payloadFileName,
        Input input,
        Options options,
        X509Certificate2 senderCert,
        X509Certificate2 recipientCert)
    {
        var messageId = $"{Guid.NewGuid()}@frends.as4";
        var attachmentCid = MimeUtils.GenerateMessageId();

        // 1 – Build SOAP envelope XML
        var soapDoc = BuildSoapEnvelope(input, messageId, attachmentCid);

        // 2 – Optionally encrypt the payload bytes
        byte[] finalPayloadBytes = payloadBytes;
        if (options.EncryptMessage && recipientCert != null)
            finalPayloadBytes = EncryptPayload(soapDoc, attachmentCid, payloadBytes, recipientCert);

        // 3 – Optionally sign the SOAP envelope + attachment digest
        if (options.SignMessage && senderCert != null)
            SignEnvelope(soapDoc, attachmentCid, finalPayloadBytes, senderCert);

        // 4 – Assemble multipart/related MIME message
        return AssembleMimeMessage(soapDoc, finalPayloadBytes, payloadFileName, attachmentCid);
    }

    // -------------------------------------------------------------------------
    // SOAP envelope construction
    // -------------------------------------------------------------------------

    private static XmlDocument BuildSoapEnvelope(Input input, string messageId, string attachmentCid)
    {
        var doc = new XmlDocument { PreserveWhitespace = false };

        // Root envelope
        var envelope = doc.CreateElement("S12", "Envelope", NsSoap12);
        envelope.SetAttribute("xmlns:S12", NsSoap12);
        envelope.SetAttribute("xmlns:eb", NsEbms);
        envelope.SetAttribute("xmlns:wsse", NsWsse);
        envelope.SetAttribute("xmlns:wsu", NsWsu);
        envelope.SetAttribute("xmlns:ds", NsDs);
        envelope.SetAttribute("xmlns:xenc", NsXenc);
        doc.AppendChild(envelope);

        // Header
        var header = doc.CreateElement("S12", "Header", NsSoap12);
        envelope.AppendChild(header);

        // eb:Messaging
        var messaging = BuildMessagingHeader(doc, input, messageId, attachmentCid);
        header.AppendChild(messaging);

        // wsse:Security (initially empty — signing/encryption populate it)
        var security = doc.CreateElement("wsse", "Security", NsWsse);
        security.SetAttribute("mustUnderstand", NsSoap12, "true");
        header.AppendChild(security);

        // Body (empty for AS4 SwA profile)
        var body = doc.CreateElement("S12", "Body", NsSoap12);
        envelope.AppendChild(body);

        return doc;
    }

    private static XmlElement BuildMessagingHeader(
        XmlDocument doc, Input input, string messageId, string attachmentCid)
    {
        var messaging = doc.CreateElement("eb", "Messaging", NsEbms);
        messaging.SetAttribute("Id", NsWsu, "Messaging");      // wsu:Id for signing reference
        messaging.SetAttribute("mustUnderstand", NsSoap12, "true");

        var userMessage = doc.CreateElement("eb", "UserMessage", NsEbms);
        messaging.AppendChild(userMessage);

        // MessageInfo
        var msgInfo = doc.CreateElement("eb", "MessageInfo", NsEbms);
        AppendTextElement(doc, msgInfo, "eb", "Timestamp", NsEbms, DateTime.UtcNow.ToString("o"));
        AppendTextElement(doc, msgInfo, "eb", "MessageId", NsEbms, messageId);
        userMessage.AppendChild(msgInfo);

        // PartyInfo
        var partyInfo = doc.CreateElement("eb", "PartyInfo", NsEbms);

        var from = doc.CreateElement("eb", "From", NsEbms);
        AppendTextElement(doc, from, "eb", "PartyId", NsEbms, input.SenderPartyId);
        AppendTextElement(doc, from, "eb", "Role", NsEbms, EbDefaultRole);
        partyInfo.AppendChild(from);

        var to = doc.CreateElement("eb", "To", NsEbms);
        AppendTextElement(doc, to, "eb", "PartyId", NsEbms, input.RecipientPartyId);
        AppendTextElement(doc, to, "eb", "Role", NsEbms, EbDefaultRole);
        partyInfo.AppendChild(to);

        userMessage.AppendChild(partyInfo);

        // CollaborationInfo
        var collab = doc.CreateElement("eb", "CollaborationInfo", NsEbms);
        if (!string.IsNullOrWhiteSpace(input.AgreementRef))
            AppendTextElement(doc, collab, "eb", "AgreementRef", NsEbms, input.AgreementRef);
        AppendTextElement(doc, collab, "eb", "Service", NsEbms, input.Service);
        AppendTextElement(doc, collab, "eb", "Action", NsEbms, input.Action);
        AppendTextElement(doc, collab, "eb", "ConversationId", NsEbms, input.ConversationId);
        userMessage.AppendChild(collab);

        // PayloadInfo
        var payloadInfo = doc.CreateElement("eb", "PayloadInfo", NsEbms);
        var partInfo = doc.CreateElement("eb", "PartInfo", NsEbms);
        partInfo.SetAttribute("href", $"cid:{attachmentCid}");

        var partProperties = doc.CreateElement("eb", "PartProperties", NsEbms);
        var mimeTypeProp = doc.CreateElement("eb", "Property", NsEbms);
        mimeTypeProp.SetAttribute("name", "MimeType");
        mimeTypeProp.InnerText = "application/octet-stream";
        partProperties.AppendChild(mimeTypeProp);
        partInfo.AppendChild(partProperties);

        payloadInfo.AppendChild(partInfo);
        userMessage.AppendChild(payloadInfo);

        return messaging;
    }

    // -------------------------------------------------------------------------
    // WS-Security: Signing
    // -------------------------------------------------------------------------

    private static void SignEnvelope(
        XmlDocument soapDoc,
        string attachmentCid,
        byte[] attachmentBytes,
        X509Certificate2 certificate)
    {
        var security = GetSecurityElement(soapDoc);

        // BinarySecurityToken
        var bstId = "BST-" + Guid.NewGuid().ToString("N");
        var bst = soapDoc.CreateElement("wsse", "BinarySecurityToken", NsWsse);
        bst.SetAttribute("Id", NsWsu, bstId);
        bst.SetAttribute("ValueType", BstValueType);
        bst.SetAttribute("EncodingType", BstEncodingType);
        bst.InnerText = Convert.ToBase64String(certificate.RawData);
        security.AppendChild(bst);

        // Compute digests
        // Reference 1: eb:Messaging element (Exclusive C14N)
        var messagingElement = GetMessagingElement(soapDoc);
        var messagingDigest = ComputeC14nDigest(messagingElement);

        // Reference 2: Attachment (raw bytes, SHA-256)
        var attachmentDigest = SHA256.HashData(attachmentBytes);

        // Build SignedInfo XML
        var signedInfoXml = BuildSignedInfoXml(messagingDigest, attachmentDigest, attachmentCid);

        // Sign the canonical SignedInfo
        var signedInfoDoc = new XmlDocument();
        signedInfoDoc.LoadXml(signedInfoXml);
        var c14nSignedInfo = CanonicalizNodeExclusive(signedInfoDoc.DocumentElement);

        using var rsa = certificate.GetRSAPrivateKey();
        var signatureValue = rsa.SignData(c14nSignedInfo, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        // Build full ds:Signature element and append to Security
        var signatureElement = BuildSignatureElement(soapDoc, signedInfoXml, signatureValue, bstId);
        security.AppendChild(signatureElement);
    }

    private static string BuildSignedInfoXml(byte[] messagingDigest, byte[] attachmentDigest, string attachmentCid)
    {
        var sb = new StringBuilder();
        sb.Append("<ds:SignedInfo xmlns:ds=\"http://www.w3.org/2000/09/xmldsig#\">");
        sb.Append("<ds:CanonicalizationMethod Algorithm=\"http://www.w3.org/2001/10/xml-exc-c14n#\"/>");
        sb.Append("<ds:SignatureMethod Algorithm=\"http://www.w3.org/2001/04/xmldsig-more#rsa-sha256\"/>");

        // Reference: eb:Messaging
        sb.Append("<ds:Reference URI=\"#Messaging\">");
        sb.Append("<ds:Transforms><ds:Transform Algorithm=\"http://www.w3.org/2001/10/xml-exc-c14n#\"/></ds:Transforms>");
        sb.Append("<ds:DigestMethod Algorithm=\"http://www.w3.org/2001/04/xmlenc#sha256\"/>");
        sb.AppendFormat("<ds:DigestValue>{0}</ds:DigestValue>", Convert.ToBase64String(messagingDigest));
        sb.Append("</ds:Reference>");

        // Reference: Attachment via CID
        sb.AppendFormat("<ds:Reference URI=\"cid:{0}\">", attachmentCid);
        sb.Append("<ds:Transforms><ds:Transform Algorithm=\"http://docs.oasis-open.org/wss/oasis-wss-SwAProfile-1.1#Attachment-Content-Signature-Transform\"/></ds:Transforms>");
        sb.Append("<ds:DigestMethod Algorithm=\"http://www.w3.org/2001/04/xmlenc#sha256\"/>");
        sb.AppendFormat("<ds:DigestValue>{0}</ds:DigestValue>", Convert.ToBase64String(attachmentDigest));
        sb.Append("</ds:Reference>");

        sb.Append("</ds:SignedInfo>");
        return sb.ToString();
    }

    private static XmlElement BuildSignatureElement(
        XmlDocument soapDoc, string signedInfoXml, byte[] signatureValue, string bstId)
    {
        var signature = soapDoc.CreateElement("ds", "Signature", NsDs);
        signature.SetAttribute("Id", "SIG-" + Guid.NewGuid().ToString("N"));

        // Import SignedInfo node
        var tempDoc = new XmlDocument();
        tempDoc.LoadXml(signedInfoXml);
        var importedSignedInfo = soapDoc.ImportNode(tempDoc.DocumentElement, true);
        signature.AppendChild(importedSignedInfo);

        // SignatureValue
        var sigValue = soapDoc.CreateElement("ds", "SignatureValue", NsDs);
        sigValue.InnerText = Convert.ToBase64String(signatureValue);
        signature.AppendChild(sigValue);

        // KeyInfo → SecurityTokenReference → Reference to BST
        var keyInfo = soapDoc.CreateElement("ds", "KeyInfo", NsDs);
        var secTokenRef = soapDoc.CreateElement("wsse", "SecurityTokenReference", NsWsse);
        var reference = soapDoc.CreateElement("wsse", "Reference", NsWsse);
        reference.SetAttribute("URI", $"#{bstId}");
        reference.SetAttribute("ValueType", BstValueType);
        secTokenRef.AppendChild(reference);
        keyInfo.AppendChild(secTokenRef);
        signature.AppendChild(keyInfo);

        return signature;
    }

    // -------------------------------------------------------------------------
    // WS-Security: Encryption
    // -------------------------------------------------------------------------

    private static byte[] EncryptPayload(
        XmlDocument soapDoc,
        string attachmentCid,
        byte[] plainBytes,
        X509Certificate2 recipientCert)
    {
        // Generate AES-256 session key + IV
        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Mode = CipherMode.CBC;
        aes.GenerateKey();
        aes.GenerateIV();

        // Encrypt payload
        using var encryptor = aes.CreateEncryptor();
        byte[] cipherBytes;
        using (var ms = new MemoryStream())
        {
            // Prepend IV so the receiver can extract it
            ms.Write(aes.IV, 0, aes.IV.Length);
            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                cs.Write(plainBytes, 0, plainBytes.Length);
            cipherBytes = ms.ToArray();
        }

        // Encrypt session key with recipient's RSA public key (OAEP-SHA1 per XML Enc spec)
        using var rsa = recipientCert.GetRSAPublicKey();
        var encryptedKey = rsa.Encrypt(aes.Key, RSAEncryptionPadding.OaepSHA1);

        // Build EncryptedKey element in the Security header
        var security = GetSecurityElement(soapDoc);
        var encKeyElement = BuildEncryptedKeyElement(soapDoc, encryptedKey, recipientCert, attachmentCid);
        security.AppendChild(encKeyElement);

        return cipherBytes;
    }

    private static XmlElement BuildEncryptedKeyElement(
        XmlDocument soapDoc,
        byte[] encryptedKey,
        X509Certificate2 recipientCert,
        string attachmentCid)
    {
        var encKeyId = "EK-" + Guid.NewGuid().ToString("N");
        var encDataId = "ED-" + Guid.NewGuid().ToString("N");

        var ek = soapDoc.CreateElement("xenc", "EncryptedKey", NsXenc);
        ek.SetAttribute("Id", encKeyId);

        // EncryptionMethod
        var encMethod = soapDoc.CreateElement("xenc", "EncryptionMethod", NsXenc);
        encMethod.SetAttribute("Algorithm", "http://www.w3.org/2001/04/xmlenc#rsa-oaep-mgf1p");
        ek.AppendChild(encMethod);

        // KeyInfo → X509Data → IssuerSerial
        var keyInfo = soapDoc.CreateElement("ds", "KeyInfo", NsDs);
        var x509Data = soapDoc.CreateElement("ds", "X509Data", NsDs);
        var issuerSerial = soapDoc.CreateElement("ds", "X509IssuerSerial", NsDs);
        AppendTextElement(soapDoc, issuerSerial, "ds", "X509IssuerName", NsDs, recipientCert.Issuer);
        AppendTextElement(soapDoc, issuerSerial, "ds", "X509SerialNumber", NsDs, recipientCert.SerialNumber);
        x509Data.AppendChild(issuerSerial);
        keyInfo.AppendChild(x509Data);
        ek.AppendChild(keyInfo);

        // CipherData
        var cipherData = soapDoc.CreateElement("xenc", "CipherData", NsXenc);
        var cipherValue = soapDoc.CreateElement("xenc", "CipherValue", NsXenc);
        cipherValue.InnerText = Convert.ToBase64String(encryptedKey);
        cipherData.AppendChild(cipherValue);
        ek.AppendChild(cipherData);

        // ReferenceList → DataReference to the attachment EncryptedData
        var refList = soapDoc.CreateElement("xenc", "ReferenceList", NsXenc);
        var dataRef = soapDoc.CreateElement("xenc", "DataReference", NsXenc);
        dataRef.SetAttribute("URI", $"#{encDataId}");
        refList.AppendChild(dataRef);
        ek.AppendChild(refList);

        // EncryptedData reference for the attachment (stored as annotation on the attachment CID mapping)
        // The actual EncryptedData lives as an attribute in the attachment MIME part content-type; we
        // embed an EncryptedData element after EncryptedKey to satisfy strict schema validators.
        var ed = soapDoc.CreateElement("xenc", "EncryptedData", NsXenc);
        ed.SetAttribute("Id", encDataId);
        ed.SetAttribute("Type", "http://docs.oasis-open.org/wss/oasis-wss-SwAProfile-1.1#Attachment-Ciphertext-Transform");
        var edMethod = soapDoc.CreateElement("xenc", "EncryptionMethod", NsXenc);
        edMethod.SetAttribute("Algorithm", "http://www.w3.org/2001/04/xmlenc#aes256-cbc");
        ed.AppendChild(edMethod);
        var edKeyInfo = soapDoc.CreateElement("ds", "KeyInfo", NsDs);
        var ekRef = soapDoc.CreateElement("wsse", "SecurityTokenReference", NsWsse);
        var ekRefEl = soapDoc.CreateElement("wsse", "Reference", NsWsse);
        ekRefEl.SetAttribute("URI", $"#{encKeyId}");
        ekRef.AppendChild(ekRefEl);
        edKeyInfo.AppendChild(ekRef);
        ed.AppendChild(edKeyInfo);
        var edCipherData = soapDoc.CreateElement("xenc", "CipherData", NsXenc);
        var attachRef = soapDoc.CreateElement("xenc", "CipherReference", NsXenc);
        attachRef.SetAttribute("URI", $"cid:{attachmentCid}");
        edCipherData.AppendChild(attachRef);
        ed.AppendChild(edCipherData);

        // Append EncryptedData as sibling after EncryptedKey inside Security
        // We return ek; the caller appends it, then we need to also append ed.
        // Use a DocumentFragment to return both nodes together.
        // Instead, append ed as a child of ek for builder convenience;
        // the receiver will find it by ID.  Some AS4 stacks allow this; otherwise
        // callers can move it to Security directly.
        // For maximum compatibility we attach it to the Security element here by
        // temporarily appending to the Security node inside this helper:
        var security = GetSecurityElement(soapDoc);
        security.AppendChild(ed);     // EncryptedData appended directly to Security

        return ek;
    }

    // -------------------------------------------------------------------------
    // MIME assembly
    // -------------------------------------------------------------------------

    private static MimeMessage AssembleMimeMessage(
        XmlDocument soapDoc,
        byte[] payloadBytes,
        string payloadFileName,
        string attachmentCid)
    {
        // Serialise SOAP envelope
        var soapXml = SerializeSoap(soapDoc);

        var multipart = new Multipart("related");
        multipart.ContentType.Parameters["type"] = "application/soap+xml";
        multipart.ContentType.Parameters["start"] = "<soap-envelope@frends>";
        multipart.ContentType.Parameters["start-info"] = "application/soap+xml";

        // Part 1: SOAP envelope
        var soapPart = new MimePart("application", "soap+xml")
        {
            Content = new MimeContent(new MemoryStream(Encoding.UTF8.GetBytes(soapXml))),
            ContentTransferEncoding = ContentEncoding.EightBit,
        };
        soapPart.ContentType.Parameters["charset"] = "UTF-8";
        soapPart.ContentType.Parameters["action"] = "\"\"";
        soapPart.ContentId = "soap-envelope@frends";
        multipart.Add(soapPart);

        // Part 2: Payload attachment
        var attachmentPart = new MimePart("application", "octet-stream")
        {
            Content = new MimeContent(new MemoryStream(payloadBytes)),
            ContentTransferEncoding = ContentEncoding.Binary,
            ContentId = attachmentCid,
        };
        attachmentPart.ContentDisposition = new ContentDisposition(ContentDisposition.Attachment)
        {
            FileName = payloadFileName,
        };
        multipart.Add(attachmentPart);

        var message = new MimeMessage();
        message.Body = multipart;
        return message;
    }

    // -------------------------------------------------------------------------
    // Utilities
    // -------------------------------------------------------------------------

    private static byte[] ComputeC14nDigest(XmlElement element)
    {
        var bytes = CanonicalizNodeExclusive(element);
        return SHA256.HashData(bytes);
    }

    private static byte[] CanonicalizNodeExclusive(XmlNode node)
    {
        // XmlDsigExcC14NTransform accepts XmlDocument, Stream, or XmlNodeList — not XmlNode/XmlElement.
        // Import the node into a fresh XmlDocument so we can pass it as XmlDocument.
        var tempDoc = new XmlDocument { PreserveWhitespace = false };
        tempDoc.AppendChild(tempDoc.ImportNode(node, true));

        var transform = new XmlDsigExcC14NTransform();
        transform.LoadInput(tempDoc);
        using var stream = (Stream)transform.GetOutput(typeof(Stream));
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    private static string SerializeSoap(XmlDocument doc)
    {
        using var sw = new StringWriter();
        using var xw = XmlWriter.Create(sw, new XmlWriterSettings { Indent = false, Encoding = Encoding.UTF8, OmitXmlDeclaration = true });
        doc.Save(xw);
        return sw.ToString();
    }

    private static XmlElement GetSecurityElement(XmlDocument doc)
    {
        var ns = new XmlNamespaceManager(doc.NameTable);
        ns.AddNamespace("wsse", NsWsse);
        return (XmlElement)doc.SelectSingleNode("//wsse:Security", ns);
    }

    private static XmlElement GetMessagingElement(XmlDocument doc)
    {
        var ns = new XmlNamespaceManager(doc.NameTable);
        ns.AddNamespace("eb", NsEbms);
        return (XmlElement)doc.SelectSingleNode("//eb:Messaging", ns);
    }

    private static void AppendTextElement(
        XmlDocument doc, XmlElement parent, string prefix, string localName, string ns, string value)
    {
        var el = doc.CreateElement(prefix, localName, ns);
        el.InnerText = value;
        parent.AppendChild(el);
    }
}
