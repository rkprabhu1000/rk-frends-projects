using System;
using System.Threading;
using System.Threading.Tasks;
using Frends.URLDownload.Dalux.Definitions;
using Frends.URLDownload.Dalux.Helpers;
using NUnit.Framework;

namespace Frends.URLDownload.Dalux.Tests;

[TestFixture]
public class DaluxFileDownloadTests : TestBase
{
    // ─────────────────────────────────────────────────────────────────────────
    // Unit tests — no external dependencies
    // ─────────────────────────────────────────────────────────────────────────

    [Test]
    public void ExtractFileName_Should_Decode_UTF8_Encoded_Filename()
    {
        var result = ContentDispositionHelper.ExtractFileName(
            "attachment; filename*=UTF-8''FloorPlan_Building%20A_Rev3.pdf");
        Assert.That(result, Is.EqualTo("FloorPlan_Building A_Rev3.pdf"));
    }

    [Test]
    public void ExtractFileName_Should_Return_Default_When_Header_Is_Empty()
    {
        var result = ContentDispositionHelper.ExtractFileName(string.Empty);
        Assert.That(result, Is.EqualTo("downloaded_file.pdf"));
    }

    [Test]
    public void ExtractFileName_Should_Return_Default_When_No_UTF8_Prefix()
    {
        var result = ContentDispositionHelper.ExtractFileName("attachment; filename=\"report.pdf\"");
        Assert.That(result, Is.EqualTo("downloaded_file.pdf"));
    }

    [Test]
    public void ExtractFileName_Should_Handle_Percent_Encoded_Special_Characters()
    {
        var result = ContentDispositionHelper.ExtractFileName(
            "attachment; filename*=UTF-8''Invoice%232025.pdf");
        Assert.That(result, Is.EqualTo("Invoice#2025.pdf"));
    }

    [Test]
    public void Download_Should_Throw_ArgumentException_When_CallingUrl_Is_Empty()
    {
        var input = new Input
        {
            CallingUrl = string.Empty,
            FileUrl = "https://example.com/file",
            Email = "user@example.com",
            Password = "pw",
        };
        var options = new Options { InstallBrowserIfMissing = false, ThrowErrorOnFailure = true };
        Assert.ThrowsAsync<ArgumentException>(
            (Func<Task>)(() => URLDownload.Dalux(input, options, CancellationToken.None)));
    }

    [Test]
    public void Download_Should_Throw_ArgumentException_When_FileUrl_Is_Empty()
    {
        var input = new Input
        {
            CallingUrl = "https://example.com/login",
            FileUrl = string.Empty,
            Email = "user@example.com",
            Password = "pw",
        };
        var options = new Options { InstallBrowserIfMissing = false, ThrowErrorOnFailure = true };
        Assert.ThrowsAsync<ArgumentException>(
            (Func<Task>)(() => URLDownload.Dalux(input, options, CancellationToken.None)));
    }

    [Test]
    public void Download_Should_Throw_ArgumentException_When_Email_Is_Empty()
    {
        var input = new Input
        {
            CallingUrl = "https://example.com/login",
            FileUrl = "https://example.com/file",
            Email = string.Empty,
            Password = "pw",
        };
        var options = new Options { InstallBrowserIfMissing = false, ThrowErrorOnFailure = true };
        Assert.ThrowsAsync<ArgumentException>(
            (Func<Task>)(() => URLDownload.Dalux(input, options, CancellationToken.None)));
    }

    [Test]
    public async Task Download_Should_Return_Error_Result_When_ThrowErrorOnFailure_Is_False()
    {
        var input = new Input
        {
            CallingUrl = "https://example.com/login",
            FileUrl = "https://example.com/file",
            Email = string.Empty,
            Password = "pw",
        };
        var options = new Options { InstallBrowserIfMissing = false, ThrowErrorOnFailure = false };
        var result = await URLDownload.Dalux(input, options, CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Is.Not.Null);
        Assert.That(result.Error.Message, Is.Not.Empty);
    }

    [Test]
    public async Task Download_Should_Use_Custom_ErrorMessage_When_ThrowErrorOnFailure_Is_False()
    {
        var input = new Input
        {
            CallingUrl = string.Empty,
            FileUrl = "https://example.com/file",
            Email = "user@example.com",
            Password = "pw",
        };
        var options = new Options
        {
            InstallBrowserIfMissing = false,
            ThrowErrorOnFailure = false,
            ErrorMessageOnFailure = "Custom error message",
        };
        var result = await URLDownload.Dalux(input, options, CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Error.Message, Is.EqualTo("Custom error message"));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Live integration test — skipped when .env is absent
    // Set DALUX_CALLING_URL, DALUX_FILE_URL, DALUX_EMAIL, DALUX_PASSWORD in .env
    // ─────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task Download_Should_Download_Real_File_From_Dalux()
    {
        if (!HasEnvVar("DALUX_CALLING_URL"))
            Assert.Ignore("DALUX_CALLING_URL not set in .env — skipping live integration test.");

        var input = new Input
        {
            CallingUrl = Env("DALUX_CALLING_URL"),
            FileUrl = Env("DALUX_FILE_URL"),
            Email = Env("DALUX_EMAIL"),
            Password = Env("DALUX_PASSWORD"),
        };

        var options = new Options
        {
            Headless = true,
            TimeoutMs = 60000,
            InstallBrowserIfMissing = true,
            ThrowErrorOnFailure = true,
        };

        var result = await URLDownload.Dalux(input, options, CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(result.FileName, Is.Not.Null.And.Not.Empty);
        Assert.That(result.Content, Is.Not.Null.And.Not.Empty);
    }
}
