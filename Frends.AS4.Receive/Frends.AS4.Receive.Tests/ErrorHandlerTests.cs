using System;
using System.Threading;
using System.Threading.Tasks;
using Frends.AS4.Receive.Definitions;
using NUnit.Framework;

namespace Frends.AS4.Receive.Tests;

[TestFixture]
public class ErrorHandlerTests : TestBase
{
    [Test]
    public void Should_Throw_When_ContentType_Is_Missing_And_ThrowOnFailure_Is_True()
    {
        var input = new Input
        {
            RequestBody = "some-body",
            ContentTypeHeader = string.Empty,
            ReceiverCertificatePath = string.Empty,
            ReceiverCertificatePassword = string.Empty,
            SenderCertificatePath = string.Empty,
        };

        var options = DefaultOptions();
        options.ThrowErrorOnFailure = true;

        var ex = Assert.ThrowsAsync<Exception>(() =>
            AS4.Receive(input, options, CancellationToken.None));

        Assert.That(ex, Is.Not.Null);
        Assert.That(ex.Message, Does.Contain("ContentTypeHeader"));
    }

    [Test]
    public async Task Should_Return_Failed_Result_When_ContentType_Is_Missing_And_ThrowOnFailure_Is_False()
    {
        var input = new Input
        {
            RequestBody = "some-body",
            ContentTypeHeader = string.Empty,
            ReceiverCertificatePath = string.Empty,
            ReceiverCertificatePassword = string.Empty,
            SenderCertificatePath = string.Empty,
        };

        var options = DefaultOptions();
        options.ThrowErrorOnFailure = false;

        var result = await AS4.Receive(input, options, CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Is.Not.Null);
        Assert.That(result.Error.AdditionalInfo, Is.Not.Null);
    }

    [Test]
    public void Should_Use_Custom_ErrorMessageOnFailure()
    {
        var input = new Input
        {
            RequestBody = string.Empty,
            ContentTypeHeader = "multipart/related",
            ReceiverCertificatePath = string.Empty,
            ReceiverCertificatePassword = string.Empty,
            SenderCertificatePath = string.Empty,
        };

        var options = DefaultOptions();
        options.ThrowErrorOnFailure = true;
        options.ErrorMessageOnFailure = "Custom receive error";

        var ex = Assert.ThrowsAsync<Exception>(() =>
            AS4.Receive(input, options, CancellationToken.None));

        Assert.That(ex.Message, Contains.Substring("Custom receive error"));
    }

    [Test]
    public void Should_Throw_When_Malformed_MIME_Body_And_ThrowOnFailure_Is_True()
    {
        var input = new Input
        {
            RequestBody = "this-is-not-a-valid-mime-body",
            ContentTypeHeader = "multipart/related; boundary=\"boundary123\"",
            ReceiverCertificatePath = string.Empty,
            ReceiverCertificatePassword = string.Empty,
            SenderCertificatePath = string.Empty,
        };

        var options = DefaultOptions();
        options.ThrowErrorOnFailure = true;

        Assert.ThrowsAsync<Exception>(() =>
            AS4.Receive(input, options, CancellationToken.None));
    }

    [Test]
    public async Task Should_Return_Failed_Result_When_Malformed_MIME_Body_And_ThrowOnFailure_Is_False()
    {
        var input = new Input
        {
            RequestBody = "this-is-not-a-valid-mime-body",
            ContentTypeHeader = "multipart/related; boundary=\"boundary123\"",
            ReceiverCertificatePath = string.Empty,
            ReceiverCertificatePassword = string.Empty,
            SenderCertificatePath = string.Empty,
        };

        var options = DefaultOptions();
        options.ThrowErrorOnFailure = false;

        var result = await AS4.Receive(input, options, CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Is.Not.Null);
    }

    private static Options DefaultOptions() => new()
    {
        VerifySignature = false,
        DecryptPayload = false,
        DecompressPayload = false,
        ThrowErrorOnFailure = true,
        ErrorMessageOnFailure = string.Empty,
    };
}
