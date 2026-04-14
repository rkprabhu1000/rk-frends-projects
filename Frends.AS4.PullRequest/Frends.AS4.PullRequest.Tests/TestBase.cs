using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using dotenv.net;

namespace Frends.AS4.PullRequest.Tests;

public abstract class TestBase
{
    protected TestBase()
    {
        DotEnv.Load();
    }

    protected static (X509Certificate2 certWithKey, byte[] pfxBytes, string pfxPassword, byte[] cerBytes)
        GenerateTestCertificate(string subjectName = "CN=AS4PullTest")
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
        var certWithKey = new X509Certificate2(pfxBytes, password, X509KeyStorageFlags.Exportable);
        return (certWithKey, pfxBytes, password, cerBytes);
    }

    protected static string WriteTempFile(byte[] bytes, string extension = ".tmp")
    {
        var path = Path.Combine(Path.GetTempPath(), $"frends_as4_pull_{Guid.NewGuid():N}{extension}");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    /// <summary>
    /// Builds a minimal valid AS4 PullRequest SOAP body (single-part, no attachment).
    /// </summary>
    protected static (string body, string contentType) BuildPullRequestMime(
        string mpc = "urn:mpc:test:declarations",
        string messageId = "pull-001@partner.example.com")
    {
        var soapXml = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<S12:Envelope xmlns:S12=""http://www.w3.org/2003/05/soap-envelope""
              xmlns:eb=""http://docs.oasis-open.org/ebxml-msg/ebms/v3.0/ns/core/20070523/"">
  <S12:Header>
    <eb:Messaging S12:mustUnderstand=""true"">
      <eb:SignalMessage>
        <eb:MessageInfo>
          <eb:Timestamp>{DateTime.UtcNow:o}</eb:Timestamp>
          <eb:MessageId>{messageId}</eb:MessageId>
        </eb:MessageInfo>
        <eb:PullRequest mpc=""{mpc}""/>
      </eb:SignalMessage>
    </eb:Messaging>
  </S12:Header>
  <S12:Body/>
</S12:Envelope>";

        var boundary = $"MIMEBoundary_{Guid.NewGuid():N}";

        var body =
            $"--{boundary}\r\n" +
            $"Content-Type: application/soap+xml; charset=UTF-8\r\n" +
            $"Content-Transfer-Encoding: 8bit\r\n\r\n" +
            $"{soapXml}\r\n" +
            $"--{boundary}--";

        var contentType =
            $"multipart/related; type=\"application/soap+xml\"; boundary=\"{boundary}\"";

        return (body, contentType);
    }
}
