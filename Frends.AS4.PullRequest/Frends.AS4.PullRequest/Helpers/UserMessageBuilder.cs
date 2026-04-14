using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml;
using Frends.AS4.PullRequest.Definitions;
using MimeKit;
using MimeKit.Utils;

namespace Frends.AS4.PullRequest.Helpers;

/// <summary>
/// Builds the synchronous AS4 UserMessage response to a PullRequest.
/// The response is a Multipart/Related MIME message containing a signed and
/// optionally encrypted SOAP envelope plus the payload attachment.
/// </summary>
internal static class UserMessageBuilder
{
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
    /// Builds a complete MIME UserMessage response from a queued payload.
    /// Returns the serialised MIME body string and the Content-Type header value.
    /// </summary>
    internal static (string mimeBody, string contentType) Build(
        byte[] payloadBytes,
        string payloadFileName,
        Input input,
        Options options,
        string pullRequestMessageId,
        X509Certificate2 senderCert,
        X509Certificate2 recipientCert)
    {
        var messageId = $"{Guid.NewGuid()}@frends.as4";
        var attachmentCid = MimeUtils.GenerateMessageId();

        // Build SOAP envelope
        var soapDoc = BuildSoapEnvelope(input, messageId, pullRequestMessageId, attachmentCid);

        // Optionally encrypt the payload
        byte[] finalPayloadBytes = payloadBytes;
        if (options.EncryptResponse && recipientCert != null)
            finalPayloadBytes = EncryptPayload(soapDoc, attachmentCid, payloadBytes, recipientCert);

        // Optionally sign the envelope
        if (options.SignResponse && senderCert != null)
            SignEnvelope(soapDoc, attachmentCid, finalPayloadBytes, senderCert);

        // Assemble MIME message
        return AssembleMime(soapDoc, finalPayloadBytes, payloadFileName, attachmentCid);
    }

    // -------------------------------------------------------------------------
    // SOAP envelope
    // -------------------------------------------------------------------------

    private static XmlDocument BuildSoapEnvelope(
        Input input, string messageId, string refToMessageId, string attachmentCid)
    {
        var doc = new XmlDocument { PreserveWhitespace = false };

        var envelope = doc.CreateElement("S12", "Envelope", NsSoap12);
        envelope.SetAttribute("xmlns:S12", NsSoap12);
        envelope.SetAttribute("xmlns:eb", NsEbms);
        envelope.SetAttribute("xmlns:wsse", NsWsse);
        envelope.SetAttribute("xmlns:wsu", NsWsu);
        envelope.SetAttribute("xmlns:ds", NsDs);
        envelope.SetAttribute("xmlns:xenc", NsXenc);
        doc.AppendChild(envelope);

        var header = doc.CreateElement("S12", "Header", NsSoap12);
        envelope.AppendChild(header);

        // eb:Messaging
        header.AppendChild(BuildMessaging(doc, input, messageId, refToMessageId, attachmentCid));

        // wsse:Security (populated by signing/encryption steps)
        var security = doc.CreateElement("wsse", "Security", NsWsse);
        security.SetAttribute("mustUnderstand", NsSoap12, "true");
        header.AppendChild(security);

        // Empty Body (AS4 SwA profile — payload is always an attachment)
        envelope.AppendChild(doc.CreateElement("S12", "Body", NsSoap12));

        return doc;
    }

    private static XmlElement BuildMessaging(
        XmlDocument doc, Input input, string messageId, string refToMessageId, string attachmentCid)
    {
        var messaging = doc.CreateElement("eb", "Messaging", NsEbms);
        messaging.SetAttribute("Id", NsWsu, "Messaging");
        messaging.SetAttribute("mustUnderstand", NsSoap12, "true");

        var userMessage = doc.CreateElement("eb", "UserMessage", NsEbms);
        messaging.AppendChild(userMessage);

        // MessageInfo — RefToMessageId links back to the PullRequest
        var msgInfo = doc.CreateElement("eb", "MessageInfo", NsEbms);
        AppendText(doc, msgInfo, "eb", "Timestamp", NsEbms, DateTime.UtcNow.ToString("o"));
        AppendText(doc, msgInfo, "eb", "MessageId", NsEbms, messageId);
        if (!string.IsNullOrWhiteSpace(refToMessageId))
            AppendText(doc, msgInfo, "eb", "RefToMessageId", NsEbms, refToMessageId);
        userMessage.AppendChild(msgInfo);

        // PartyInfo
        var partyInfo = doc.CreateElement("eb", "PartyInfo", NsEbms);
        var from = doc.CreateElement("eb", "From", NsEbms);
        AppendText(doc, from, "eb", "PartyId", NsEbms, input.SenderPartyId);
        AppendText(doc, from, "eb", "Role", NsEbms, EbDefaultRole);
        partyInfo.AppendChild(from);
        var to = doc.CreateElement("eb", "To", NsEbms);
        AppendText(doc, to, "eb", "PartyId", NsEbms, input.RecipientPartyId);
        AppendText(doc, to, "eb", "Role", NsEbms, EbDefaultRole);
        partyInfo.AppendChild(to);
        userMessage.AppendChild(partyInfo);

        // CollaborationInfo
        var collab = doc.CreateElement("eb", "CollaborationInfo", NsEbms);
        if (!string.IsNullOrWhiteSpace(input.AgreementRef))
            AppendText(doc, collab, "eb", "AgreementRef", NsEbms, input.AgreementRef);
        AppendText(doc, collab, "eb", "Service", NsEbms, input.Service);
        AppendText(doc, collab, "eb", "Action", NsEbms, input.Action);
        AppendText(doc, collab, "eb", "ConversationId", NsEbms, input.ConversationId);
        userMessage.AppendChild(collab);

        // PayloadInfo
        var payloadInfo = doc.CreateElement("eb", "PayloadInfo", NsEbms);
        var partInfo = doc.CreateElement("eb", "PartInfo", NsEbms);
        partInfo.SetAttribute("href", $"cid:{attachmentCid}");
        var partProps = doc.CreateElement("eb", "PartProperties", NsEbms);
        var mimeTypeProp = doc.CreateElement("eb", "Property", NsEbms);
        mimeTypeProp.SetAttribute("name", "MimeType");
        mimeTypeProp.InnerText = "application/octet-stream";
        partProps.AppendChild(mimeTypeProp);
        partInfo.AppendChild(partProps);
        payloadInfo.AppendChild(partInfo);
        userMessage.AppendChild(payloadInfo);

        return messaging;
    }

    // -------------------------------------------------------------------------
    // Signing
    // -------------------------------------------------------------------------

    private static void SignEnvelope(
        XmlDocument soapDoc, string attachmentCid, byte[] attachmentBytes, X509Certificate2 cert)
    {
        var security = GetSecurity(soapDoc);

        var bstId = "BST-" + Guid.NewGuid().ToString("N");
        var bst = soapDoc.CreateElement("wsse", "BinarySecurityToken", NsWsse);
        bst.SetAttribute("Id", NsWsu, bstId);
        bst.SetAttribute("ValueType", BstValueType);
        bst.SetAttribute("EncodingType", BstEncodingType);
        bst.InnerText = Convert.ToBase64String(cert.RawData);
        security.AppendChild(bst);

        var messagingDigest = ComputeC14nDigest(GetMessaging(soapDoc));
        var attachmentDigest = SHA256.HashData(attachmentBytes);

        var signedInfoXml = BuildSignedInfoXml(messagingDigest, attachmentDigest, attachmentCid);

        var siDoc = new XmlDocument();
        siDoc.LoadXml(signedInfoXml);
        var c14nSi = CanonicalizNode(siDoc.DocumentElement);

        using var rsa = cert.GetRSAPrivateKey();
        var sigValue = rsa.SignData(c14nSi, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        security.AppendChild(BuildSignatureElement(soapDoc, signedInfoXml, sigValue, bstId));
    }

    private static string BuildSignedInfoXml(byte[] messagingDigest, byte[] attachmentDigest, string attachmentCid)
    {
        var sb = new StringBuilder();
        sb.Append("<ds:SignedInfo xmlns:ds=\"http://www.w3.org/2000/09/xmldsig#\">");
        sb.Append("<ds:CanonicalizationMethod Algorithm=\"http://www.w3.org/2001/10/xml-exc-c14n#\"/>");
        sb.Append("<ds:SignatureMethod Algorithm=\"http://www.w3.org/2001/04/xmldsig-more#rsa-sha256\"/>");

        sb.Append("<ds:Reference URI=\"#Messaging\">");
        sb.Append("<ds:Transforms><ds:Transform Algorithm=\"http://www.w3.org/2001/10/xml-exc-c14n#\"/></ds:Transforms>");
        sb.Append("<ds:DigestMethod Algorithm=\"http://www.w3.org/2001/04/xmlenc#sha256\"/>");
        sb.AppendFormat("<ds:DigestValue>{0}</ds:DigestValue>", Convert.ToBase64String(messagingDigest));
        sb.Append("</ds:Reference>");

        sb.AppendFormat("<ds:Reference URI=\"cid:{0}\">", attachmentCid);
        sb.Append("<ds:Transforms><ds:Transform Algorithm=\"http://docs.oasis-open.org/wss/oasis-wss-SwAProfile-1.1#Attachment-Content-Signature-Transform\"/></ds:Transforms>");
        sb.Append("<ds:DigestMethod Algorithm=\"http://www.w3.org/2001/04/xmlenc#sha256\"/>");
        sb.AppendFormat("<ds:DigestValue>{0}</ds:DigestValue>", Convert.ToBase64String(attachmentDigest));
        sb.Append("</ds:Reference>");

        sb.Append("</ds:SignedInfo>");
        return sb.ToString();
    }

    private static XmlElement BuildSignatureElement(
        XmlDocument soapDoc, string signedInfoXml, byte[] sigValue, string bstId)
    {
        var sig = soapDoc.CreateElement("ds", "Signature", NsDs);

        var tempDoc = new XmlDocument();
        tempDoc.LoadXml(signedInfoXml);
        sig.AppendChild(soapDoc.ImportNode(tempDoc.DocumentElement, true));

        var sv = soapDoc.CreateElement("ds", "SignatureValue", NsDs);
        sv.InnerText = Convert.ToBase64String(sigValue);
        sig.AppendChild(sv);

        var keyInfo = soapDoc.CreateElement("ds", "KeyInfo", NsDs);
        var str = soapDoc.CreateElement("wsse", "SecurityTokenReference", NsWsse);
        var refEl = soapDoc.CreateElement("wsse", "Reference", NsWsse);
        refEl.SetAttribute("URI", $"#{bstId}");
        refEl.SetAttribute("ValueType", BstValueType);
        str.AppendChild(refEl);
        keyInfo.AppendChild(str);
        sig.AppendChild(keyInfo);

        return sig;
    }

    // -------------------------------------------------------------------------
    // Encryption
    // -------------------------------------------------------------------------

    private static byte[] EncryptPayload(
        XmlDocument soapDoc, string attachmentCid, byte[] plainBytes, X509Certificate2 recipientCert)
    {
        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Mode = CipherMode.CBC;
        aes.GenerateKey();
        aes.GenerateIV();

        byte[] cipher;
        using (var ms = new MemoryStream())
        {
            ms.Write(aes.IV, 0, aes.IV.Length);
            using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                cs.Write(plainBytes, 0, plainBytes.Length);
            cipher = ms.ToArray();
        }

        using var rsa = recipientCert.GetRSAPublicKey();
        var wrappedKey = rsa.Encrypt(aes.Key, RSAEncryptionPadding.OaepSHA1);

        var security = GetSecurity(soapDoc);
        var (ekEl, edEl) = BuildEncryptedKeyElements(soapDoc, wrappedKey, recipientCert, attachmentCid);
        security.AppendChild(ekEl);
        security.AppendChild(edEl);

        return cipher;
    }

    private static (XmlElement ek, XmlElement ed) BuildEncryptedKeyElements(
        XmlDocument doc, byte[] wrappedKey, X509Certificate2 recipientCert, string attachmentCid)
    {
        var ekId = "EK-" + Guid.NewGuid().ToString("N");
        var edId = "ED-" + Guid.NewGuid().ToString("N");

        // EncryptedKey
        var ek = doc.CreateElement("xenc", "EncryptedKey", NsXenc);
        ek.SetAttribute("Id", ekId);
        var ekMethod = doc.CreateElement("xenc", "EncryptionMethod", NsXenc);
        ekMethod.SetAttribute("Algorithm", "http://www.w3.org/2001/04/xmlenc#rsa-oaep-mgf1p");
        ek.AppendChild(ekMethod);

        var keyInfo = doc.CreateElement("ds", "KeyInfo", NsDs);
        var x509Data = doc.CreateElement("ds", "X509Data", NsDs);
        var issuerSerial = doc.CreateElement("ds", "X509IssuerSerial", NsDs);
        AppendText(doc, issuerSerial, "ds", "X509IssuerName", NsDs, recipientCert.Issuer);
        AppendText(doc, issuerSerial, "ds", "X509SerialNumber", NsDs, recipientCert.SerialNumber);
        x509Data.AppendChild(issuerSerial);
        keyInfo.AppendChild(x509Data);
        ek.AppendChild(keyInfo);

        var ekCipherData = doc.CreateElement("xenc", "CipherData", NsXenc);
        var ekCipherValue = doc.CreateElement("xenc", "CipherValue", NsXenc);
        ekCipherValue.InnerText = Convert.ToBase64String(wrappedKey);
        ekCipherData.AppendChild(ekCipherValue);
        ek.AppendChild(ekCipherData);

        var refList = doc.CreateElement("xenc", "ReferenceList", NsXenc);
        var dataRef = doc.CreateElement("xenc", "DataReference", NsXenc);
        dataRef.SetAttribute("URI", $"#{edId}");
        refList.AppendChild(dataRef);
        ek.AppendChild(refList);

        // EncryptedData (CipherReference points to the attachment CID)
        var ed = doc.CreateElement("xenc", "EncryptedData", NsXenc);
        ed.SetAttribute("Id", edId);
        ed.SetAttribute("Type", "http://docs.oasis-open.org/wss/oasis-wss-SwAProfile-1.1#Attachment-Ciphertext-Transform");
        var edMethod = doc.CreateElement("xenc", "EncryptionMethod", NsXenc);
        edMethod.SetAttribute("Algorithm", "http://www.w3.org/2001/04/xmlenc#aes256-cbc");
        ed.AppendChild(edMethod);
        var edKeyInfo = doc.CreateElement("ds", "KeyInfo", NsDs);
        var secTokenRef = doc.CreateElement("wsse", "SecurityTokenReference", NsWsse);
        var ekRef = doc.CreateElement("wsse", "Reference", NsWsse);
        ekRef.SetAttribute("URI", $"#{ekId}");
        secTokenRef.AppendChild(ekRef);
        edKeyInfo.AppendChild(secTokenRef);
        ed.AppendChild(edKeyInfo);
        var edCipherData = doc.CreateElement("xenc", "CipherData", NsXenc);
        var cipherRef = doc.CreateElement("xenc", "CipherReference", NsXenc);
        cipherRef.SetAttribute("URI", $"cid:{attachmentCid}");
        edCipherData.AppendChild(cipherRef);
        ed.AppendChild(edCipherData);

        return (ek, ed);
    }

    // -------------------------------------------------------------------------
    // MIME assembly
    // -------------------------------------------------------------------------

    private static (string mimeBody, string contentType) AssembleMime(
        XmlDocument soapDoc, byte[] payloadBytes, string payloadFileName, string attachmentCid)
    {
        var soapXml = Serialize(soapDoc);
        var boundary = $"MIMEBoundary_{Guid.NewGuid():N}";
        var payloadB64 = Convert.ToBase64String(payloadBytes);

        var mimeBody =
            $"--{boundary}\r\n" +
            $"Content-Type: application/soap+xml; charset=UTF-8\r\n" +
            $"Content-Transfer-Encoding: 8bit\r\n\r\n" +
            $"{soapXml}\r\n" +
            $"--{boundary}\r\n" +
            $"Content-Type: application/octet-stream\r\n" +
            $"Content-Transfer-Encoding: base64\r\n" +
            $"Content-ID: <{attachmentCid}>\r\n" +
            $"Content-Disposition: attachment; filename=\"{payloadFileName}\"\r\n\r\n" +
            $"{payloadB64}\r\n" +
            $"--{boundary}--";

        var contentType =
            $"multipart/related; type=\"application/soap+xml\"; " +
            $"start=\"<soap-envelope@frends>\"; boundary=\"{boundary}\"";

        return (mimeBody, contentType);
    }

    // -------------------------------------------------------------------------
    // Utilities
    // -------------------------------------------------------------------------

    private static byte[] ComputeC14nDigest(XmlElement element)
        => SHA256.HashData(CanonicalizNode(element));

    private static byte[] CanonicalizNode(XmlNode node)
    {
        var tempDoc = new XmlDocument { PreserveWhitespace = false };
        tempDoc.AppendChild(tempDoc.ImportNode(node, true));
        var transform = new XmlDsigExcC14NTransform();
        transform.LoadInput(tempDoc);
        using var stream = (Stream)transform.GetOutput(typeof(Stream));
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    private static string Serialize(XmlDocument doc)
    {
        using var sw = new StringWriter();
        using var xw = XmlWriter.Create(sw, new XmlWriterSettings
        {
            Indent = false,
            Encoding = Encoding.UTF8,
            OmitXmlDeclaration = true,
        });
        doc.Save(xw);
        return sw.ToString();
    }

    private static XmlElement GetSecurity(XmlDocument doc)
    {
        var ns = new XmlNamespaceManager(doc.NameTable);
        ns.AddNamespace("wsse", NsWsse);
        return (XmlElement)doc.SelectSingleNode("//wsse:Security", ns);
    }

    private static XmlElement GetMessaging(XmlDocument doc)
    {
        var ns = new XmlNamespaceManager(doc.NameTable);
        ns.AddNamespace("eb", NsEbms);
        return (XmlElement)doc.SelectSingleNode("//eb:Messaging", ns);
    }

    private static void AppendText(XmlDocument doc, XmlElement parent,
        string prefix, string localName, string ns, string value)
    {
        var el = doc.CreateElement(prefix, localName, ns);
        el.InnerText = value;
        parent.AppendChild(el);
    }
}
