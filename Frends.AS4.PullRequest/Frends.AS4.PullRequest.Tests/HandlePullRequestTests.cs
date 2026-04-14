using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Frends.AS4.PullRequest.Definitions;
using Frends.AS4.PullRequest.Helpers;
using NUnit.Framework;

namespace Frends.AS4.PullRequest.Tests;

[TestFixture]
public class PullRequestTests : TestBase
{
    private X509Certificate2 _senderCert;
    private string _senderPfxPath;
    private string _senderPfxPassword;
    private string _recipientCerPath;

    [SetUp]
    public void SetUp()
    {
        var (certWithKey, pfxBytes, password, cerBytes) = GenerateTestCertificate("CN=AS4PullSender");
        _senderCert = certWithKey;
        _senderPfxPassword = password;
        _senderPfxPath = WriteTempFile(pfxBytes, ".pfx");
        _recipientCerPath = WriteTempFile(cerBytes, ".cer");
    }

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(_senderPfxPath)) File.Delete(_senderPfxPath);
        if (File.Exists(_recipientCerPath)) File.Delete(_recipientCerPath);
    }

    // -------------------------------------------------------------------------
    // PullRequest parsing
    // -------------------------------------------------------------------------

    [Test]
    public async Task Should_Extract_MPC_And_MessageId_From_PullRequest()
    {
        var (body, ct) = BuildPullRequestMime(
            mpc: "urn:mpc:customs:declarations",
            messageId: "pull-001@partner.test");

        var result = await AS4.PullRequest(
            BuildInput(body, ct),
            BuildOptions(sign: false, encrypt: false),
            CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(result.RequestedMpc, Is.EqualTo("urn:mpc:customs:declarations"));
        Assert.That(result.PullRequestMessageId, Is.EqualTo("pull-001@partner.test"));
    }

    [Test]
    public void PullRequestParser_Should_Throw_When_No_PullRequest_Element()
    {
        // A UserMessage body rather than a PullRequest
        var notAPullRequest = $@"<?xml version=""1.0""?>
<S12:Envelope xmlns:S12=""http://www.w3.org/2003/05/soap-envelope""
              xmlns:eb=""http://docs.oasis-open.org/ebxml-msg/ebms/v3.0/ns/core/20070523/"">
  <S12:Header>
    <eb:Messaging S12:mustUnderstand=""true"">
      <eb:UserMessage>
        <eb:MessageInfo><eb:MessageId>msg@test</eb:MessageId></eb:MessageInfo>
      </eb:UserMessage>
    </eb:Messaging>
  </S12:Header>
  <S12:Body/>
</S12:Envelope>";

        var doc = new XmlDocument();
        doc.LoadXml(notAPullRequest);

        Assert.Throws<FormatException>(() => PullRequestParser.ExtractPullRequestFields(doc));
    }

    // -------------------------------------------------------------------------
    // Empty queue (EBMS:0006) response
    // -------------------------------------------------------------------------

    [Test]
    public async Task Should_Return_EmptyQueue_Signal_When_PayloadFilePath_Is_Empty()
    {
        var (body, ct) = BuildPullRequestMime("urn:mpc:test", "pull-empty@test");

        var input = BuildInput(body, ct);
        input.PayloadFilePath = string.Empty;    // explicitly no payload

        var result = await AS4.PullRequest(
            input, BuildOptions(sign: false, encrypt: false), CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(result.IsEmptyQueue, Is.True);
        Assert.That(result.ResponseBody, Does.Contain("EBMS:0006"));
        Assert.That(result.ResponseBody, Does.Contain("EmptyMessagePartitionChannel"));
        Assert.That(result.ResponseBody, Does.Contain("pull-empty@test"));
    }

    [Test]
    public async Task EmptyQueue_Response_Should_Reference_PullRequest_MessageId()
    {
        var (body, ct) = BuildPullRequestMime("urn:mpc:test", "pull-ref-check@test");
        var input = BuildInput(body, ct);
        input.PayloadFilePath = string.Empty;

        var result = await AS4.PullRequest(
            input, BuildOptions(sign: false, encrypt: false), CancellationToken.None);

        // refToMessageInError attribute should contain the PullRequest's MessageId
        Assert.That(result.ResponseBody, Does.Contain("pull-ref-check@test"));
    }

    [Test]
    public void EmptyQueueSignalBuilder_Should_Return_Valid_Soap_Xml()
    {
        var (mimeBody, contentType) = EmptyQueueSignalBuilder.Build("urn:mpc:test", "msg-001@test");

        Assert.That(contentType, Does.Contain("multipart/related"));
        Assert.That(mimeBody, Does.Contain("EBMS:0006"));
        Assert.That(mimeBody, Does.Contain("EmptyMessagePartitionChannel"));
        Assert.That(mimeBody, Does.Contain("warning"));
        Assert.That(mimeBody, Does.Contain("urn:mpc:test"));
    }

    // -------------------------------------------------------------------------
    // UserMessage response — structure
    // -------------------------------------------------------------------------

    [Test]
    public async Task Should_Return_UserMessage_Response_When_Payload_Is_Provided()
    {
        var payloadPath = WriteTempFile(Encoding.UTF8.GetBytes("<Declaration>test</Declaration>"), ".xml");
        try
        {
            var (body, ct) = BuildPullRequestMime("urn:mpc:customs", "pull-002@partner.test");
            var input = BuildInput(body, ct, payloadPath);

            var result = await AS4.PullRequest(
                input, BuildOptions(sign: false, encrypt: false), CancellationToken.None);

            Assert.That(result.Success, Is.True);
            Assert.That(result.IsEmptyQueue, Is.False);
            Assert.That(result.ResponseBody, Does.Contain("eb:UserMessage"));
            Assert.That(result.ResponseBody, Does.Contain("eb:Messaging"));
            Assert.That(result.ResponseContentType, Does.Contain("multipart/related"));
        }
        finally
        {
            File.Delete(payloadPath);
        }
    }

    [Test]
    public async Task UserMessage_Should_Contain_RefToMessageId_Linking_To_PullRequest()
    {
        var payloadPath = WriteTempFile(Encoding.UTF8.GetBytes("<Data/>"), ".xml");
        try
        {
            var (body, ct) = BuildPullRequestMime("urn:mpc:test", "pull-ref-003@test");
            var input = BuildInput(body, ct, payloadPath);

            var result = await AS4.PullRequest(
                input, BuildOptions(sign: false, encrypt: false), CancellationToken.None);

            Assert.That(result.ResponseBody, Does.Contain("pull-ref-003@test"));
        }
        finally
        {
            File.Delete(payloadPath);
        }
    }

    [Test]
    public async Task UserMessage_Should_Contain_Routing_Metadata()
    {
        var payloadPath = WriteTempFile(Encoding.UTF8.GetBytes("<Data/>"), ".xml");
        try
        {
            var (body, ct) = BuildPullRequestMime("urn:mpc:test", "pull-004@test");
            var input = BuildInput(body, ct, payloadPath);
            input.Service = "urn:services:CustomsService";
            input.Action = "SubmitDeclaration";
            input.ConversationId = "conv-pull-004";
            input.AgreementRef = "urn:agreements:PMode-Pull-v1";

            var result = await AS4.PullRequest(
                input, BuildOptions(sign: false, encrypt: false), CancellationToken.None);

            Assert.That(result.ResponseBody, Does.Contain("urn:services:CustomsService"));
            Assert.That(result.ResponseBody, Does.Contain("SubmitDeclaration"));
            Assert.That(result.ResponseBody, Does.Contain("conv-pull-004"));
            Assert.That(result.ResponseBody, Does.Contain("urn:agreements:PMode-Pull-v1"));
        }
        finally
        {
            File.Delete(payloadPath);
        }
    }

    // -------------------------------------------------------------------------
    // UserMessage response — signing
    // -------------------------------------------------------------------------

    [Test]
    public async Task UserMessage_Should_Include_WsSecurity_Signature_When_SignResponse_Is_True()
    {
        var payloadPath = WriteTempFile(Encoding.UTF8.GetBytes("<Invoice/>"), ".xml");
        try
        {
            var (body, ct) = BuildPullRequestMime();
            var input = BuildInput(body, ct, payloadPath, _senderPfxPath, _senderPfxPassword);

            var result = await AS4.PullRequest(
                input, BuildOptions(sign: true, encrypt: false), CancellationToken.None);

            Assert.That(result.ResponseBody, Does.Contain("ds:Signature"));
            Assert.That(result.ResponseBody, Does.Contain("BinarySecurityToken"));
            Assert.That(result.ResponseBody, Does.Contain("rsa-sha256"));
        }
        finally
        {
            File.Delete(payloadPath);
        }
    }

    [Test]
    public async Task UserMessage_Should_Not_Include_Signature_When_SignResponse_Is_False()
    {
        var payloadPath = WriteTempFile(Encoding.UTF8.GetBytes("<Invoice/>"), ".xml");
        try
        {
            var (body, ct) = BuildPullRequestMime();
            var input = BuildInput(body, ct, payloadPath);

            var result = await AS4.PullRequest(
                input, BuildOptions(sign: false, encrypt: false), CancellationToken.None);

            Assert.That(result.ResponseBody, Does.Not.Contain("ds:Signature"));
        }
        finally
        {
            File.Delete(payloadPath);
        }
    }

    // -------------------------------------------------------------------------
    // UserMessage response — encryption
    // -------------------------------------------------------------------------

    [Test]
    public async Task UserMessage_Should_Include_EncryptedKey_When_EncryptResponse_Is_True()
    {
        var payloadPath = WriteTempFile(Encoding.UTF8.GetBytes("SensitiveDeclaration"), ".xml");
        try
        {
            var (body, ct) = BuildPullRequestMime();
            var input = BuildInput(body, ct, payloadPath,
                senderPfxPath: string.Empty,
                senderPfxPassword: string.Empty,
                recipientCerPath: _recipientCerPath);

            var result = await AS4.PullRequest(
                input, BuildOptions(sign: false, encrypt: true), CancellationToken.None);

            Assert.That(result.ResponseBody, Does.Contain("xenc:EncryptedKey"));
            Assert.That(result.ResponseBody, Does.Contain("xenc:EncryptedData"));
            Assert.That(result.ResponseBody, Does.Contain("aes256-cbc"));
        }
        finally
        {
            File.Delete(payloadPath);
        }
    }

    // -------------------------------------------------------------------------
    // Error handling
    // -------------------------------------------------------------------------

    [Test]
    public void Should_Throw_When_RequestBody_Is_Empty_And_ThrowOnFailure_Is_True()
    {
        var input = BuildInput(string.Empty, "multipart/related");
        var options = BuildOptions(sign: false, encrypt: false);
        options.ThrowErrorOnFailure = true;

        var ex = Assert.ThrowsAsync<Exception>(() =>
            AS4.PullRequest(input, options, CancellationToken.None));
        Assert.That(ex.Message, Does.Contain("RequestBody"));
    }

    [Test]
    public async Task Should_Return_Failed_Result_When_RequestBody_Is_Empty_And_ThrowOnFailure_Is_False()
    {
        var input = BuildInput(string.Empty, "multipart/related");
        var options = BuildOptions(sign: false, encrypt: false);
        options.ThrowErrorOnFailure = false;

        var result = await AS4.PullRequest(input, options, CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Is.Not.Null);
    }

    [Test]
    public void Should_Throw_When_Payload_File_Does_Not_Exist()
    {
        var (body, ct) = BuildPullRequestMime();
        var input = BuildInput(body, ct, "/nonexistent/payload.xml");

        Assert.ThrowsAsync<Exception>(() =>
            AS4.PullRequest(input, BuildOptions(sign: false, encrypt: false), CancellationToken.None));
    }

    [Test]
    public void Should_Use_Custom_ErrorMessageOnFailure()
    {
        var input = BuildInput(string.Empty, "multipart/related");
        var options = BuildOptions(sign: false, encrypt: false);
        options.ThrowErrorOnFailure = true;
        options.ErrorMessageOnFailure = "Custom pull error";

        var ex = Assert.ThrowsAsync<Exception>(() =>
            AS4.PullRequest(input, options, CancellationToken.None));
        Assert.That(ex.Message, Does.Contain("Custom pull error"));
    }

    [Test]
    public void Should_Propagate_Cancellation()
    {
        var (body, ct) = BuildPullRequestMime();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAsync<Exception>(() =>
            AS4.PullRequest(BuildInput(body, ct), BuildOptions(sign: false, encrypt: false), cts.Token));
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static Input BuildInput(
        string body,
        string contentType,
        string payloadPath = "",
        string senderPfxPath = "",
        string senderPfxPassword = "",
        string recipientCerPath = "") => new()
    {
        RequestBody = body,
        ContentTypeHeader = contentType,
        PayloadFilePath = payloadPath,
        SenderPartyId = "urn:sender:test",
        RecipientPartyId = "urn:recipient:test",
        Service = "urn:test:service",
        Action = "TestAction",
        ConversationId = "conv-pull-test",
        AgreementRef = string.Empty,
        SenderCertificatePath = senderPfxPath,
        SenderCertificatePassword = senderPfxPassword,
        RecipientCertificatePath = recipientCerPath,
    };

    private static Options BuildOptions(bool sign, bool encrypt) => new()
    {
        SignResponse = sign,
        EncryptResponse = encrypt,
        ThrowErrorOnFailure = true,
        ErrorMessageOnFailure = string.Empty,
    };
}
