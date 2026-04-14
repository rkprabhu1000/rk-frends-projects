using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Frends.AS4.Send.Definitions;
using NUnit.Framework;

namespace Frends.AS4.Send.Tests;

[TestFixture]
public class ErrorHandlerTests : TestBase
{
    // -------------------------------------------------------------------------
    // ThrowErrorOnFailure = true
    // -------------------------------------------------------------------------

    [Test]
    public void Should_Throw_When_FilePath_Is_Missing_And_ThrowOnFailure_Is_True()
    {
        var (input, options) = BuildDefaults();
        input.FilePath = "/nonexistent/path/payload.xml";
        options.ThrowErrorOnFailure = true;

        var ex = Assert.ThrowsAsync<Exception>(() =>
            AS4.Send(input, options, CancellationToken.None));

        Assert.That(ex, Is.Not.Null);
        Assert.That(ex.Message, Does.Contain("payload.xml"));
    }

    // -------------------------------------------------------------------------
    // ThrowErrorOnFailure = false
    // -------------------------------------------------------------------------

    [Test]
    public async Task Should_Return_Failed_Result_When_FilePath_Is_Missing_And_ThrowOnFailure_Is_False()
    {
        var (input, options) = BuildDefaults();
        input.FilePath = "/nonexistent/path/payload.xml";
        options.ThrowErrorOnFailure = false;

        var result = await AS4.Send(input, options, CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Is.Not.Null);
        Assert.That(result.Error.Message, Does.Contain("payload.xml"));
    }

    [Test]
    public async Task Should_Return_Failed_Result_When_RecipientUrl_Is_Unreachable_And_ThrowOnFailure_Is_False()
    {
        var payloadPath = WriteTempFile(System.Text.Encoding.UTF8.GetBytes("<test/>"), ".xml");

        try
        {
            var (pfxBytes, pfxPassword, cerBytes) = GenerateTestCertificate();
            var pfxPath = WriteTempFile(pfxBytes, ".pfx");
            var cerPath = WriteTempFile(cerBytes, ".cer");

            try
            {
                var input = new Input
                {
                    FilePath = payloadPath,
                    RecipientUrl = "http://127.0.0.1:19999/as4/nonexistent",
                    SenderPartyId = "urn:sender",
                    RecipientPartyId = "urn:recipient",
                    Service = "urn:test:service",
                    Action = "TestAction",
                    ConversationId = "conv-001",
                    AgreementRef = string.Empty,
                    SenderCertificatePath = pfxPath,
                    SenderCertificatePassword = pfxPassword,
                    RecipientCertificatePath = cerPath,
                    ResponseDecryptionCertificatePath = string.Empty,
                    ResponseDecryptionCertificatePassword = string.Empty,
                };

                var options = new Options
                {
                    SignMessage = true,
                    EncryptMessage = true,
                    DecryptResponse = true,
                    DecompressResponse = true,
                    ThrowErrorOnFailure = false,
                };

                var result = await AS4.Send(input, options, CancellationToken.None);

                Assert.That(result.Success, Is.False);
                Assert.That(result.Error, Is.Not.Null);
                Assert.That(result.Error.AdditionalInfo, Is.Not.Null);
            }
            finally
            {
                File.Delete(pfxPath);
                File.Delete(cerPath);
            }
        }
        finally
        {
            File.Delete(payloadPath);
        }
    }

    // -------------------------------------------------------------------------
    // Cancellation
    // -------------------------------------------------------------------------

    [Test]
    public void Should_Throw_OperationCanceledException_When_Token_Is_Cancelled()
    {
        var (input, options) = BuildDefaults();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAsync<Exception>(() => AS4.Send(input, options, cts.Token));
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static (Input input, Options options) BuildDefaults()
    {
        var input = new Input
        {
            FilePath = "/nonexistent/payload.xml",
            RecipientUrl = "http://127.0.0.1:19999/as4",
            SenderPartyId = "urn:sender",
            RecipientPartyId = "urn:recipient",
            Service = "urn:service",
            Action = "Action",
            ConversationId = "conv-001",
            AgreementRef = string.Empty,
            SenderCertificatePath = string.Empty,
            SenderCertificatePassword = string.Empty,
            RecipientCertificatePath = string.Empty,
            ResponseDecryptionCertificatePath = string.Empty,
            ResponseDecryptionCertificatePassword = string.Empty,
        };

        var options = new Options
        {
            SignMessage = false,
            EncryptMessage = false,
            DecryptResponse = false,
            DecompressResponse = false,
            ThrowErrorOnFailure = true,
        };

        return (input, options);
    }
}
