using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using dotenv.net;

namespace Frends.AS4.Receive.Tests;

/// <summary>
/// Provides self-signed test certificates and MIME message factories for unit tests.
/// All tests run without external services.
/// </summary>
public abstract class TestBase
{
    protected TestBase()
    {
        DotEnv.Load();
    }

    // -------------------------------------------------------------------------
    // Test certificate factory
    // -------------------------------------------------------------------------

    protected static (X509Certificate2 certWithKey, byte[] pfxBytes, string pfxPassword, byte[] cerBytes)
        GenerateTestCertificate(string subjectName = "CN=AS4ReceiveTest")
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(subjectName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.DataEncipherment | X509KeyUsageFlags.KeyEncipherment,
            critical: false));

        var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(5));
        var password = "TestPassword123!";
        var pfxBytes = cert.Export(X509ContentType.Pfx, password);
        var cerBytes = cert.Export(X509ContentType.Cert);

        // Reload from PFX so the private key is properly accessible on all platforms
        var certWithKey = new X509Certificate2(pfxBytes, password, X509KeyStorageFlags.Exportable);
        return (certWithKey, pfxBytes, password, cerBytes);
    }

    protected static string WriteTempFile(byte[] bytes, string extension = ".tmp")
    {
        var path = Path.Combine(Path.GetTempPath(), $"frends_as4_rcv_{Guid.NewGuid():N}{extension}");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    // -------------------------------------------------------------------------
    // MIME message factory — produces the same format that As4MessageBuilder outputs
    // -------------------------------------------------------------------------

    /// <summary>
    /// Builds a minimal valid Multipart/Related AS4 MIME body string and its Content-Type header,
    /// optionally encrypting the attachment with <paramref name="recipientCert"/>.
    /// </summary>
    protected static (string mimeBody, string contentType) BuildTestMimeMessage(
        string payloadXml,
        string messageId,
        string senderPartyId,
        string recipientPartyId,
        string service,
        string action,
        string conversationId,
        string agreementRef = null,
        X509Certificate2 senderCert = null,
        X509Certificate2 recipientCert = null)
    {
        var attachmentCid = $"payload-{Guid.NewGuid():N}@test";
        var payloadBytes = Encoding.UTF8.GetBytes(payloadXml);

        var agreementRefXml = agreementRef != null
            ? $"<eb:AgreementRef>{agreementRef}</eb:AgreementRef>"
            : string.Empty;

        // Build the BinarySecurityToken if a senderCert is provided (for signature testing)
        var bstXml = senderCert != null
            ? $"<wsse:BinarySecurityToken wsu:Id=\"BST1\" " +
              $"ValueType=\"http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-x509-token-profile-1.0#X509v3\" " +
              $"EncodingType=\"http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-soap-message-security-1.0#Base64Binary\">" +
              $"{Convert.ToBase64String(senderCert.RawData)}</wsse:BinarySecurityToken>"
            : string.Empty;

        // Encrypt payload if recipient cert provided
        byte[] finalPayloadBytes = payloadBytes;
        var encryptedKeyXml = string.Empty;
        var encryptedDataXml = string.Empty;

        if (recipientCert != null)
        {
            (finalPayloadBytes, encryptedKeyXml, encryptedDataXml) =
                BuildEncryptedPayload(payloadBytes, attachmentCid, recipientCert);
        }

        var soapEnvelope = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<S12:Envelope xmlns:S12=""http://www.w3.org/2003/05/soap-envelope""
              xmlns:eb=""http://docs.oasis-open.org/ebxml-msg/ebms/v3.0/ns/core/20070523/""
              xmlns:wsse=""http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd""
              xmlns:wsu=""http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd""
              xmlns:ds=""http://www.w3.org/2000/09/xmldsig#""
              xmlns:xenc=""http://www.w3.org/2001/04/xmlenc#"">
  <S12:Header>
    <eb:Messaging wsu:Id=""Messaging"" S12:mustUnderstand=""true"">
      <eb:UserMessage>
        <eb:MessageInfo>
          <eb:Timestamp>{DateTime.UtcNow:o}</eb:Timestamp>
          <eb:MessageId>{messageId}</eb:MessageId>
        </eb:MessageInfo>
        <eb:PartyInfo>
          <eb:From>
            <eb:PartyId>{senderPartyId}</eb:PartyId>
            <eb:Role>http://docs.oasis-open.org/ebxml-msg/ebms/v3.0/ns/core/20070523/defaultRole</eb:Role>
          </eb:From>
          <eb:To>
            <eb:PartyId>{recipientPartyId}</eb:PartyId>
            <eb:Role>http://docs.oasis-open.org/ebxml-msg/ebms/v3.0/ns/core/20070523/defaultRole</eb:Role>
          </eb:To>
        </eb:PartyInfo>
        <eb:CollaborationInfo>
          {agreementRefXml}
          <eb:Service>{service}</eb:Service>
          <eb:Action>{action}</eb:Action>
          <eb:ConversationId>{conversationId}</eb:ConversationId>
        </eb:CollaborationInfo>
        <eb:PayloadInfo>
          <eb:PartInfo href=""cid:{attachmentCid}"">
            <eb:PartProperties>
              <eb:Property name=""MimeType"">application/xml</eb:Property>
            </eb:PartProperties>
          </eb:PartInfo>
        </eb:PayloadInfo>
      </eb:UserMessage>
    </eb:Messaging>
    <wsse:Security S12:mustUnderstand=""true"">
      {bstXml}
      {encryptedKeyXml}
      {encryptedDataXml}
    </wsse:Security>
  </S12:Header>
  <S12:Body/>
</S12:Envelope>";

        var boundary = $"MIMEBoundary_{Guid.NewGuid():N}";
        var attachmentB64 = Convert.ToBase64String(finalPayloadBytes);

        var mimeBody =
            $"--{boundary}\r\n" +
            $"Content-Type: application/soap+xml; charset=UTF-8\r\n" +
            $"Content-Transfer-Encoding: 8bit\r\n\r\n" +
            $"{soapEnvelope}\r\n" +
            $"--{boundary}\r\n" +
            $"Content-Type: application/octet-stream\r\n" +
            $"Content-Transfer-Encoding: base64\r\n" +
            $"Content-ID: <{attachmentCid}>\r\n\r\n" +
            $"{attachmentB64}\r\n" +
            $"--{boundary}--";

        var contentType =
            $"multipart/related; type=\"application/soap+xml\"; " +
            $"start=\"<soap-envelope@test>\"; boundary=\"{boundary}\"";

        return (mimeBody, contentType);
    }

    private static (byte[] cipher, string encKeyXml, string encDataXml) BuildEncryptedPayload(
        byte[] plainBytes, string attachmentCid, X509Certificate2 recipientCert)
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
            using var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write);
            cs.Write(plainBytes, 0, plainBytes.Length);
            cs.FlushFinalBlock();
            cipher = ms.ToArray();
        }

        using var rsa = recipientCert.GetRSAPublicKey();
        var wrappedKey = rsa.Encrypt(aes.Key, RSAEncryptionPadding.OaepSHA1);

        var ekId = $"EK-{Guid.NewGuid():N}";
        var edId = $"ED-{Guid.NewGuid():N}";

        var encKeyXml =
            $"<xenc:EncryptedKey Id=\"{ekId}\">" +
            $"<xenc:EncryptionMethod Algorithm=\"http://www.w3.org/2001/04/xmlenc#rsa-oaep-mgf1p\"/>" +
            $"<xenc:CipherData><xenc:CipherValue>{Convert.ToBase64String(wrappedKey)}</xenc:CipherValue></xenc:CipherData>" +
            $"<xenc:ReferenceList><xenc:DataReference URI=\"#{edId}\"/></xenc:ReferenceList>" +
            $"</xenc:EncryptedKey>";

        var encDataXml =
            $"<xenc:EncryptedData Id=\"{edId}\" " +
            $"Type=\"http://docs.oasis-open.org/wss/oasis-wss-SwAProfile-1.1#Attachment-Ciphertext-Transform\">" +
            $"<xenc:EncryptionMethod Algorithm=\"http://www.w3.org/2001/04/xmlenc#aes256-cbc\"/>" +
            $"<xenc:CipherData><xenc:CipherReference URI=\"cid:{attachmentCid}\"/></xenc:CipherData>" +
            $"</xenc:EncryptedData>";

        return (cipher, encKeyXml, encDataXml);
    }
}
