using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Frends.AS4.Send.Definitions;
using Frends.AS4.Send.Helpers;
using NUnit.Framework;

namespace Frends.AS4.Send.Tests;

/// <summary>
/// Unit tests for the AS4 message-building and response-parsing helpers.
/// Tests that require a live AS4 endpoint are guarded by <see cref="TestBase.LiveEndpointAvailable"/>.
/// </summary>
[TestFixture]
public class SendTests : TestBase
{
    private string _pfxPath;
    private string _pfxPassword;
    private string _cerPath;
    private byte[] _pfxBytes;
    private byte[] _cerBytes;

    [SetUp]
    public void SetUp()
    {
        var (pfxBytes, password, cerBytes) = GenerateTestCertificate("CN=FrendsAS4Test");
        _pfxBytes = pfxBytes;
        _pfxPassword = password;
        _cerBytes = cerBytes;
        _pfxPath = WriteTempFile(pfxBytes, ".pfx");
        _cerPath = WriteTempFile(cerBytes, ".cer");
    }

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(_pfxPath)) File.Delete(_pfxPath);
        if (File.Exists(_cerPath)) File.Delete(_cerPath);
    }

    // -------------------------------------------------------------------------
    // Message builder: SOAP structure
    // -------------------------------------------------------------------------

    [Test]
    public void MessageBuilder_Should_Produce_Multipart_With_Two_Parts()
    {
        var payload = Encoding.UTF8.GetBytes("<Invoice>test</Invoice>");
        var input = BuildInput(_pfxPath, _pfxPassword, _cerPath);
        var options = BuildOptions(sign: false, encrypt: false);

        var senderCert = new X509Certificate2(_pfxPath, _pfxPassword, X509KeyStorageFlags.Exportable);
        var recipientCert = new X509Certificate2(_cerPath);

        var mime = As4MessageBuilder.Build(payload, "invoice.xml", input, options, senderCert, recipientCert);

        // Should be Multipart/Related
        Assert.That(mime.Body, Is.InstanceOf<MimeKit.Multipart>());
        var multipart = (MimeKit.Multipart)mime.Body;
        Assert.That(multipart.Count, Is.EqualTo(2));   // SOAP + attachment
        Assert.That(multipart[0].ContentType.MimeType, Is.EqualTo("application/soap+xml").IgnoreCase);
    }

    [Test]
    public void MessageBuilder_Should_Embed_EbMessaging_Header_With_Correct_Service()
    {
        var payload = Encoding.UTF8.GetBytes("<data/>");
        var input = BuildInput(_pfxPath, _pfxPassword, _cerPath);
        input.Service = "urn:test:MyService";

        var options = BuildOptions(sign: false, encrypt: false);
        var senderCert = new X509Certificate2(_pfxPath, _pfxPassword, X509KeyStorageFlags.Exportable);
        var recipientCert = new X509Certificate2(_cerPath);

        var mime = As4MessageBuilder.Build(payload, "data.xml", input, options, senderCert, recipientCert);

        var multipart = (MimeKit.Multipart)mime.Body;
        using var ms = new MemoryStream();
        ((MimeKit.MimePart)multipart[0]).Content.DecodeTo(ms);
        var soapXml = Encoding.UTF8.GetString(ms.ToArray());

        Assert.That(soapXml, Does.Contain("urn:test:MyService"));
        Assert.That(soapXml, Does.Contain("eb:Messaging"));
        Assert.That(soapXml, Does.Contain("wsse:Security"));
    }

    [Test]
    public void MessageBuilder_Should_Include_AgreementRef_When_Provided()
    {
        var payload = Encoding.UTF8.GetBytes("<data/>");
        var input = BuildInput(_pfxPath, _pfxPassword, _cerPath);
        input.AgreementRef = "urn:agreements:PMode-v1";

        var options = BuildOptions(sign: false, encrypt: false);
        var senderCert = new X509Certificate2(_pfxPath, _pfxPassword, X509KeyStorageFlags.Exportable);
        var recipientCert = new X509Certificate2(_cerPath);

        var mime = As4MessageBuilder.Build(payload, "data.xml", input, options, senderCert, recipientCert);
        var soapXml = GetSoapPartXml(mime);

        Assert.That(soapXml, Does.Contain("urn:agreements:PMode-v1"));
    }

    [Test]
    public void MessageBuilder_Should_Omit_AgreementRef_When_Not_Provided()
    {
        var payload = Encoding.UTF8.GetBytes("<data/>");
        var input = BuildInput(_pfxPath, _pfxPassword, _cerPath);
        input.AgreementRef = string.Empty;

        var options = BuildOptions(sign: false, encrypt: false);
        var senderCert = new X509Certificate2(_pfxPath, _pfxPassword, X509KeyStorageFlags.Exportable);
        var recipientCert = new X509Certificate2(_cerPath);

        var mime = As4MessageBuilder.Build(payload, "data.xml", input, options, senderCert, recipientCert);
        var soapXml = GetSoapPartXml(mime);

        Assert.That(soapXml, Does.Not.Contain("AgreementRef"));
    }

    // -------------------------------------------------------------------------
    // Message builder: Signing
    // -------------------------------------------------------------------------

    [Test]
    public void MessageBuilder_With_Signing_Should_Include_WsSecurity_Signature()
    {
        var payload = Encoding.UTF8.GetBytes("<Invoice/>");
        var input = BuildInput(_pfxPath, _pfxPassword, _cerPath);
        var options = BuildOptions(sign: true, encrypt: false);
        var senderCert = new X509Certificate2(_pfxPath, _pfxPassword, X509KeyStorageFlags.Exportable);
        var recipientCert = new X509Certificate2(_cerPath);

        var mime = As4MessageBuilder.Build(payload, "invoice.xml", input, options, senderCert, recipientCert);
        var soapXml = GetSoapPartXml(mime);

        Assert.That(soapXml, Does.Contain("ds:Signature"));
        Assert.That(soapXml, Does.Contain("BinarySecurityToken"));
        Assert.That(soapXml, Does.Contain("rsa-sha256"));
    }

    [Test]
    public void MessageBuilder_With_Signing_Should_Reference_Attachment_CID()
    {
        var payload = Encoding.UTF8.GetBytes("<Invoice/>");
        var input = BuildInput(_pfxPath, _pfxPassword, _cerPath);
        var options = BuildOptions(sign: true, encrypt: false);
        var senderCert = new X509Certificate2(_pfxPath, _pfxPassword, X509KeyStorageFlags.Exportable);
        var recipientCert = new X509Certificate2(_cerPath);

        var mime = As4MessageBuilder.Build(payload, "invoice.xml", input, options, senderCert, recipientCert);
        var soapXml = GetSoapPartXml(mime);

        // The SOAP envelope must reference the attachment CID from the MIME part
        var multipart = (MimeKit.Multipart)mime.Body;
        var attachCid = ((MimeKit.MimePart)multipart[1]).ContentId;
        Assert.That(soapXml, Does.Contain(attachCid));
    }

    // -------------------------------------------------------------------------
    // Message builder: Encryption
    // -------------------------------------------------------------------------

    [Test]
    public void MessageBuilder_With_Encryption_Should_Include_EncryptedKey_In_Security()
    {
        var payload = Encoding.UTF8.GetBytes("SensitiveData");
        var input = BuildInput(_pfxPath, _pfxPassword, _cerPath);
        var options = BuildOptions(sign: false, encrypt: true);
        var senderCert = new X509Certificate2(_pfxPath, _pfxPassword, X509KeyStorageFlags.Exportable);
        var recipientCert = new X509Certificate2(_cerPath);

        var mime = As4MessageBuilder.Build(payload, "data.bin", input, options, senderCert, recipientCert);
        var soapXml = GetSoapPartXml(mime);

        Assert.That(soapXml, Does.Contain("xenc:EncryptedKey"));
        Assert.That(soapXml, Does.Contain("xenc:EncryptedData"));
        Assert.That(soapXml, Does.Contain("aes256-cbc"));
    }

    [Test]
    public void MessageBuilder_With_Encryption_Should_Change_Attachment_Bytes()
    {
        var payload = Encoding.UTF8.GetBytes("OriginalPlaintext");
        var input = BuildInput(_pfxPath, _pfxPassword, _cerPath);
        var options = BuildOptions(sign: false, encrypt: true);
        var senderCert = new X509Certificate2(_pfxPath, _pfxPassword, X509KeyStorageFlags.Exportable);
        var recipientCert = new X509Certificate2(_cerPath);

        var mime = As4MessageBuilder.Build(payload, "data.bin", input, options, senderCert, recipientCert);
        var attachmentBytes = GetAttachmentBytes(mime);

        Assert.That(attachmentBytes, Is.Not.EqualTo(payload));
    }

    // -------------------------------------------------------------------------
    // Response parser: decompression
    // -------------------------------------------------------------------------

    [Test]
    public void ResponseParser_TryDecompress_Should_Inflate_GZip_Payload()
    {
        var original = Encoding.UTF8.GetBytes("<Response>data</Response>");

        using var ms = new MemoryStream();
        using (var gz = new System.IO.Compression.GZipStream(ms, System.IO.Compression.CompressionMode.Compress))
            gz.Write(original, 0, original.Length);
        var compressed = ms.ToArray();

        // Verify it looks like GZip
        Assert.That(compressed[0], Is.EqualTo(0x1F));
        Assert.That(compressed[1], Is.EqualTo(0x8B));

        // TryDecompress should detect the magic bytes and inflate
        var decompressed = As4ResponseParser.TryDecompress(compressed);
        Assert.That(decompressed, Is.EqualTo(original));
    }

    [Test]
    public void ResponseParser_TryDecompress_Should_Return_PlainBytes_Unchanged()
    {
        var plain = Encoding.UTF8.GetBytes("<Response>uncompressed</Response>");
        var result = As4ResponseParser.TryDecompress(plain);
        Assert.That(result, Is.EqualTo(plain));
    }

    // -------------------------------------------------------------------------
    // Round-trip: encrypt → decrypt session key
    // -------------------------------------------------------------------------

    [Test]
    public void EncryptDecrypt_SessionKey_Round_Trip_Should_Recover_Plaintext()
    {
        var original = Encoding.UTF8.GetBytes("Hello AS4 World!");

        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Mode = CipherMode.CBC;
        aes.GenerateKey();
        aes.GenerateIV();

        // Encrypt
        byte[] cipher;
        using (var encMs = new MemoryStream())
        {
            encMs.Write(aes.IV, 0, aes.IV.Length);
            using var cs = new System.Security.Cryptography.CryptoStream(
                encMs, aes.CreateEncryptor(), System.Security.Cryptography.CryptoStreamMode.Write);
            cs.Write(original, 0, original.Length);
            cs.FlushFinalBlock();
            cipher = encMs.ToArray();
        }

        var iv = cipher[..16];
        var cipherText = cipher[16..];

        // Decrypt via helper
        var recovered = As4ResponseParser.DecryptWithSessionKey(cipherText, iv, aes.Key);

        Assert.That(recovered, Is.EqualTo(original));
    }

    // -------------------------------------------------------------------------
    // Integration test (live endpoint only)
    // -------------------------------------------------------------------------

    [Test]
    [Category("Integration")]
    public async Task Send_Should_Return_Success_Against_Live_Endpoint()
    {
        // This test requires FRENDS_AS4_ENDPOINT to be set in .env.
        // Set up an AS4 MSH (e.g., AS4.NET, Holodeck B2B) listening on that URL.
        if (!LiveEndpointAvailable)
        {
            Assert.Ignore("FRENDS_AS4_ENDPOINT not configured – skipping integration test.");
            return;
        }

        var payloadPath = WriteTempFile(Encoding.UTF8.GetBytes("<TestPayload/>"), ".xml");
        try
        {
            var input = new Input
            {
                FilePath = payloadPath,
                RecipientUrl = As4Endpoint,
                SenderPartyId = "urn:frends:test:sender",
                RecipientPartyId = "urn:frends:test:recipient",
                Service = "urn:frends:test:service",
                Action = "TestSend",
                ConversationId = $"conv-{Guid.NewGuid():N}",
                AgreementRef = string.Empty,
                SenderCertificatePath = _pfxPath,
                SenderCertificatePassword = _pfxPassword,
                RecipientCertificatePath = _cerPath,
                ResponseDecryptionCertificatePath = string.Empty,
                ResponseDecryptionCertificatePassword = string.Empty,
            };

            var options = new Options
            {
                SignMessage = true,
                EncryptMessage = true,
                DecryptResponse = true,
                DecompressResponse = true,
                ThrowErrorOnFailure = true,
            };

            var result = await AS4.Send(input, options, CancellationToken.None);

            Assert.That(result.Success, Is.True);
            Assert.That(result.ResponseHeaders, Is.Not.Null);
        }
        finally
        {
            File.Delete(payloadPath);
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static Input BuildInput(string pfxPath, string pfxPassword, string cerPath) => new()
    {
        FilePath = string.Empty,          // overridden in individual tests
        RecipientUrl = "http://127.0.0.1:19999/as4",
        SenderPartyId = "urn:sender:test",
        RecipientPartyId = "urn:recipient:test",
        Service = "urn:test:service",
        Action = "TestAction",
        ConversationId = "conv-unit-test",
        AgreementRef = string.Empty,
        SenderCertificatePath = pfxPath,
        SenderCertificatePassword = pfxPassword,
        RecipientCertificatePath = cerPath,
        ResponseDecryptionCertificatePath = string.Empty,
        ResponseDecryptionCertificatePassword = string.Empty,
    };

    private static Options BuildOptions(bool sign, bool encrypt) => new()
    {
        SignMessage = sign,
        EncryptMessage = encrypt,
        DecryptResponse = false,
        DecompressResponse = false,
        ThrowErrorOnFailure = true,
    };

    private static string GetSoapPartXml(MimeKit.MimeMessage mime)
    {
        var multipart = (MimeKit.Multipart)mime.Body;
        using var ms = new MemoryStream();
        ((MimeKit.MimePart)multipart[0]).Content.DecodeTo(ms);
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static byte[] GetAttachmentBytes(MimeKit.MimeMessage mime)
    {
        var multipart = (MimeKit.Multipart)mime.Body;
        using var ms = new MemoryStream();
        ((MimeKit.MimePart)multipart[1]).Content.DecodeTo(ms);
        return ms.ToArray();
    }
}
