using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Frends.AS4.Receive.Definitions;
using Frends.AS4.Receive.Helpers;
using NUnit.Framework;

namespace Frends.AS4.Receive.Tests;

[TestFixture]
public class ReceiveTests : TestBase
{
    private X509Certificate2 _receiverCert;
    private byte[] _receiverPfxBytes;
    private string _receiverPfxPassword;
    private string _receiverPfxPath;

    [SetUp]
    public void SetUp()
    {
        var (certWithKey, pfxBytes, password, _) = GenerateTestCertificate("CN=AS4Receiver");
        _receiverCert = certWithKey;
        _receiverPfxBytes = pfxBytes;
        _receiverPfxPassword = password;
        _receiverPfxPath = WriteTempFile(pfxBytes, ".pfx");
    }

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(_receiverPfxPath)) File.Delete(_receiverPfxPath);
    }

    // -------------------------------------------------------------------------
    // MIME parsing + metadata extraction
    // -------------------------------------------------------------------------

    [Test]
    public async Task Receive_Should_Extract_Routing_Metadata_From_EbMessaging()
    {
        var (body, ct) = BuildTestMimeMessage(
            payloadXml: "<Invoice>test</Invoice>",
            messageId: "msg-001@test",
            senderPartyId: "urn:sender:acme",
            recipientPartyId: "urn:recipient:partner",
            service: "urn:services:InvoiceService",
            action: "Deliver",
            conversationId: "conv-001",
            agreementRef: "urn:agreements:PMode-v1");

        var result = await AS4.Receive(BuildInput(body, ct), BuildOptions(verify: false, decrypt: false), CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(result.MessageId, Is.EqualTo("msg-001@test"));
        Assert.That(result.SenderPartyId, Is.EqualTo("urn:sender:acme"));
        Assert.That(result.RecipientPartyId, Is.EqualTo("urn:recipient:partner"));
        Assert.That(result.Service, Is.EqualTo("urn:services:InvoiceService"));
        Assert.That(result.Action, Is.EqualTo("Deliver"));
        Assert.That(result.ConversationId, Is.EqualTo("conv-001"));
        Assert.That(result.AgreementRef, Is.EqualTo("urn:agreements:PMode-v1"));
    }

    [Test]
    public async Task Receive_Should_Return_Null_AgreementRef_When_Not_Present()
    {
        var (body, ct) = BuildTestMimeMessage(
            payloadXml: "<Data/>",
            messageId: "msg-002@test",
            senderPartyId: "urn:sender",
            recipientPartyId: "urn:recipient",
            service: "urn:svc",
            action: "Test",
            conversationId: "conv-002",
            agreementRef: null);

        var result = await AS4.Receive(BuildInput(body, ct), BuildOptions(verify: false, decrypt: false), CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(result.AgreementRef, Is.Null.Or.Empty);
    }

    [Test]
    public async Task Receive_Should_Return_Payload_As_String_And_Bytes()
    {
        var payloadXml = "<Order><Id>42</Id></Order>";
        var (body, ct) = BuildTestMimeMessage(
            payloadXml: payloadXml,
            messageId: "msg-003@test",
            senderPartyId: "urn:sender",
            recipientPartyId: "urn:recipient",
            service: "urn:svc",
            action: "Test",
            conversationId: "conv-003");

        var result = await AS4.Receive(BuildInput(body, ct), BuildOptions(verify: false, decrypt: false), CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(result.PayloadString, Is.EqualTo(payloadXml));
        Assert.That(result.PayloadBytes, Is.EqualTo(Encoding.UTF8.GetBytes(payloadXml)));
    }

    // -------------------------------------------------------------------------
    // Decryption
    // -------------------------------------------------------------------------

    [Test]
    public async Task Receive_Should_Decrypt_Encrypted_Payload()
    {
        var plainXml = "<Invoice><Amount>100.00</Amount></Invoice>";

        var (body, ct) = BuildTestMimeMessage(
            payloadXml: plainXml,
            messageId: "msg-enc-001@test",
            senderPartyId: "urn:sender",
            recipientPartyId: "urn:recipient",
            service: "urn:svc",
            action: "Deliver",
            conversationId: "conv-enc-001",
            recipientCert: _receiverCert);   // encrypt with receiver's public key

        var result = await AS4.Receive(
            BuildInput(body, ct, _receiverPfxPath, _receiverPfxPassword),
            BuildOptions(verify: false, decrypt: true),
            CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(result.PayloadString, Is.EqualTo(plainXml));
    }

    [Test]
    public async Task Receive_Should_Return_Raw_Bytes_When_DecryptPayload_Is_False()
    {
        var plainXml = "<Test/>";

        var (body, ct) = BuildTestMimeMessage(
            payloadXml: plainXml,
            messageId: "msg-enc-002@test",
            senderPartyId: "urn:sender",
            recipientPartyId: "urn:recipient",
            service: "urn:svc",
            action: "Test",
            conversationId: "conv-enc-002",
            recipientCert: _receiverCert);

        var result = await AS4.Receive(
            BuildInput(body, ct, _receiverPfxPath, _receiverPfxPassword),
            BuildOptions(verify: false, decrypt: false),
            CancellationToken.None);

        // With decryption disabled the bytes should NOT equal the original plaintext
        Assert.That(result.Success, Is.True);
        Assert.That(result.PayloadBytes, Is.Not.EqualTo(Encoding.UTF8.GetBytes(plainXml)));
    }

    // -------------------------------------------------------------------------
    // Decompression
    // -------------------------------------------------------------------------

    [Test]
    public void Parser_TryDecompress_Should_Inflate_GZip_Bytes()
    {
        var original = Encoding.UTF8.GetBytes("<Compressed>payload</Compressed>");

        using var ms = new MemoryStream();
        using (var gz = new System.IO.Compression.GZipStream(ms, System.IO.Compression.CompressionMode.Compress))
            gz.Write(original, 0, original.Length);
        var compressed = ms.ToArray();

        var result = As4MessageParser.TryDecompress(compressed);
        Assert.That(result, Is.EqualTo(original));
    }

    [Test]
    public void Parser_TryDecompress_Should_Return_PlainBytes_Unchanged()
    {
        var plain = Encoding.UTF8.GetBytes("<Plain>data</Plain>");
        var result = As4MessageParser.TryDecompress(plain);
        Assert.That(result, Is.EqualTo(plain));
    }

    // -------------------------------------------------------------------------
    // eb:Messaging metadata parsing (unit-level, no MIME overhead)
    // -------------------------------------------------------------------------

    [Test]
    public void Parser_Should_Extract_All_Metadata_Fields()
    {
        var soapXml = @"<S12:Envelope
            xmlns:S12=""http://www.w3.org/2003/05/soap-envelope""
            xmlns:eb=""http://docs.oasis-open.org/ebxml-msg/ebms/v3.0/ns/core/20070523/""
            xmlns:wsse=""http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd""
            xmlns:wsu=""http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd"">
          <S12:Header>
            <eb:Messaging>
              <eb:UserMessage>
                <eb:MessageInfo>
                  <eb:MessageId>unit-msg-001@test</eb:MessageId>
                </eb:MessageInfo>
                <eb:PartyInfo>
                  <eb:From><eb:PartyId>urn:from</eb:PartyId></eb:From>
                  <eb:To><eb:PartyId>urn:to</eb:PartyId></eb:To>
                </eb:PartyInfo>
                <eb:CollaborationInfo>
                  <eb:AgreementRef>urn:agreement</eb:AgreementRef>
                  <eb:Service>urn:svc</eb:Service>
                  <eb:Action>Act</eb:Action>
                  <eb:ConversationId>conv-unit</eb:ConversationId>
                </eb:CollaborationInfo>
              </eb:UserMessage>
            </eb:Messaging>
          </S12:Header>
          <S12:Body/>
        </S12:Envelope>";

        var doc = new XmlDocument();
        doc.LoadXml(soapXml);

        var meta = As4MessageParser.ExtractMessagingMetadata(doc);

        Assert.That(meta.MessageId, Is.EqualTo("unit-msg-001@test"));
        Assert.That(meta.SenderPartyId, Is.EqualTo("urn:from"));
        Assert.That(meta.RecipientPartyId, Is.EqualTo("urn:to"));
        Assert.That(meta.Service, Is.EqualTo("urn:svc"));
        Assert.That(meta.Action, Is.EqualTo("Act"));
        Assert.That(meta.ConversationId, Is.EqualTo("conv-unit"));
        Assert.That(meta.AgreementRef, Is.EqualTo("urn:agreement"));
    }

    // -------------------------------------------------------------------------
    // Decrypt session key round-trip
    // -------------------------------------------------------------------------

    [Test]
    public void DecryptWithSessionKey_Round_Trip_Should_Recover_Plaintext()
    {
        var original = Encoding.UTF8.GetBytes("AS4 Receive Decryption Test");

        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Mode = CipherMode.CBC;
        aes.GenerateKey();
        aes.GenerateIV();

        byte[] cipher;
        using (var encMs = new MemoryStream())
        {
            encMs.Write(aes.IV, 0, aes.IV.Length);
            using var cs = new CryptoStream(encMs, aes.CreateEncryptor(), CryptoStreamMode.Write);
            cs.Write(original, 0, original.Length);
            cs.FlushFinalBlock();
            cipher = encMs.ToArray();
        }

        var iv = cipher[..16];
        var cipherText = cipher[16..];
        var recovered = As4MessageParser.DecryptWithSessionKey(cipherText, iv, aes.Key);

        Assert.That(recovered, Is.EqualTo(original));
    }

    // -------------------------------------------------------------------------
    // Error handling
    // -------------------------------------------------------------------------

    [Test]
    public async Task Receive_Should_Throw_When_RequestBody_Is_Empty_And_ThrowOnFailure_Is_True()
    {
        var input = BuildInput(string.Empty, "multipart/related");
        var options = BuildOptions(verify: false, decrypt: false);
        options.ThrowErrorOnFailure = true;

        var ex = Assert.ThrowsAsync<Exception>(() => AS4.Receive(input, options, CancellationToken.None));
        Assert.That(ex.Message, Does.Contain("RequestBody"));
    }

    [Test]
    public async Task Receive_Should_Return_Failed_Result_When_RequestBody_Is_Empty_And_ThrowOnFailure_Is_False()
    {
        var input = BuildInput(string.Empty, "multipart/related");
        var options = BuildOptions(verify: false, decrypt: false);
        options.ThrowErrorOnFailure = false;

        var result = await AS4.Receive(input, options, CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Is.Not.Null);
        Assert.That(result.Error.Message, Does.Contain("RequestBody"));
    }

    [Test]
    public void Receive_Should_Propagate_Cancellation()
    {
        var (body, ct) = BuildTestMimeMessage(
            "<Data/>", "msg@test", "urn:s", "urn:r", "urn:svc", "Act", "conv");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAsync<Exception>(() =>
            AS4.Receive(BuildInput(body, ct), BuildOptions(verify: false, decrypt: false), cts.Token));
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static Input BuildInput(
        string body,
        string contentType,
        string certPath = "",
        string certPassword = "") => new()
    {
        RequestBody = body,
        ContentTypeHeader = contentType,
        ReceiverCertificatePath = certPath,
        ReceiverCertificatePassword = certPassword,
        SenderCertificatePath = string.Empty,
    };

    private static Options BuildOptions(bool verify, bool decrypt) => new()
    {
        VerifySignature = verify,
        DecryptPayload = decrypt,
        DecompressPayload = false,
        ThrowErrorOnFailure = true,
        ErrorMessageOnFailure = string.Empty,
    };
}
