using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using dotenv.net;

namespace Frends.AS4.Send.Tests;

/// <summary>
/// Base class that loads .env variables and provides self-signed test certificates.
/// Tests requiring a live AS4 endpoint set FRENDS_AS4_ENDPOINT in the .env file.
/// </summary>
public abstract class TestBase
{
    protected TestBase()
    {
        DotEnv.Load();
        As4Endpoint = Environment.GetEnvironmentVariable("FRENDS_AS4_ENDPOINT");
    }

    /// <summary>URL of a live AS4 MSH endpoint, loaded from FRENDS_AS4_ENDPOINT env var. Null in CI.</summary>
    protected string As4Endpoint { get; }

    /// <summary>True when a live endpoint is configured and integration tests should run.</summary>
    protected bool LiveEndpointAvailable => !string.IsNullOrWhiteSpace(As4Endpoint);

    // -------------------------------------------------------------------------
    // Test certificate factory
    // -------------------------------------------------------------------------

    /// <summary>
    /// Generates an in-memory RSA-2048 self-signed X.509 certificate suitable for
    /// unit tests that exercise signing and encryption paths without touching the file system.
    /// The returned PFX bytes can be saved to a temp file and loaded by the task.
    /// </summary>
    protected static (byte[] pfxBytes, string password, byte[] cerBytes) GenerateTestCertificate(string subjectName = "CN=AS4TestCert")
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
        return (pfxBytes, password, cerBytes);
    }

    /// <summary>
    /// Writes bytes to a temp file and returns the path. The caller is responsible for deleting it.
    /// </summary>
    protected static string WriteTempFile(byte[] bytes, string extension = ".tmp")
    {
        var path = Path.Combine(Path.GetTempPath(), $"frends_as4_test_{Guid.NewGuid():N}{extension}");
        File.WriteAllBytes(path, bytes);
        return path;
    }
}
