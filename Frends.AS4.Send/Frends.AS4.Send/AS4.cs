using System;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Frends.AS4.Send.Definitions;
using Frends.AS4.Send.Helpers;

namespace Frends.AS4.Send;

/// <summary>
/// Task class for AS4 Send operations.
/// </summary>
public static class AS4
{
    // Shared HttpClient; SSL errors for self-signed certs are handled by the caller providing trusted certs.
    private static readonly HttpClient HttpClient = new();

    /// <summary>
    /// Sends a file to a remote AS4 Message Service Handler (MSH) using the SOAP-with-Attachments
    /// (Multipart/Related) profile, with optional WS-Security signing and AES-256 encryption.
    /// The synchronous HTTP response is parsed, and the primary MIME attachment is extracted,
    /// optionally decrypted and decompressed.
    /// [Documentation](https://tasks.frends.com/tasks/CHANNEL/Frends.AS4.Send)
    /// </summary>
    /// <param name="input">
    /// Required connection and routing parameters: file path, recipient URL, party IDs,
    /// AS4 collaboration info (Service, Action, ConversationId, AgreementRef), and certificate paths.
    /// </param>
    /// <param name="options">
    /// Behavioural flags: SignMessage, EncryptMessage, DecryptResponse, DecompressResponse,
    /// and ThrowErrorOnFailure.
    /// </param>
    /// <param name="cancellationToken">A cancellation token provided by the Frends Platform.</param>
    /// <returns>
    /// object {
    ///   bool Success,
    ///   string PayloadString,
    ///   byte[] PayloadBytes,
    ///   Dictionary&lt;string,string&gt; ResponseHeaders,
    ///   object Error { string Message, Exception AdditionalInfo }
    /// }
    /// </returns>
    public static async Task<Result> Send(
        [PropertyTab] Input input,
        [PropertyTab] Options options,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Load payload file
            if (!File.Exists(input.FilePath))
                throw new FileNotFoundException($"Payload file not found: {input.FilePath}", input.FilePath);

            var payloadBytes = await File.ReadAllBytesAsync(input.FilePath, cancellationToken);
            var payloadFileName = Path.GetFileName(input.FilePath);

            cancellationToken.ThrowIfCancellationRequested();

            // Load certificates
            var senderCert = LoadPfxCertificate(input.SenderCertificatePath, input.SenderCertificatePassword, "sender");
            var recipientCert = LoadPublicCertificate(input.RecipientCertificatePath, "recipient");

            // Determine decryption cert (defaults to sender cert)
            X509Certificate2 decryptionCert;
            if (!string.IsNullOrWhiteSpace(input.ResponseDecryptionCertificatePath))
                decryptionCert = LoadPfxCertificate(
                    input.ResponseDecryptionCertificatePath,
                    input.ResponseDecryptionCertificatePassword,
                    "response decryption");
            else
                decryptionCert = senderCert;

            // Build AS4 MIME message
            var mimeMessage = As4MessageBuilder.Build(
                payloadBytes,
                payloadFileName,
                input,
                options,
                senderCert,
                recipientCert);

            cancellationToken.ThrowIfCancellationRequested();

            // Serialise to byte array for HTTP transport
            using var bodyStream = new MemoryStream();
            await mimeMessage.WriteToAsync(bodyStream, cancellationToken);
            var bodyBytes = bodyStream.ToArray();

            // Build HTTP request
            using var request = new HttpRequestMessage(HttpMethod.Post, input.RecipientUrl);
            var content = new ByteArrayContent(bodyBytes);

            // Set Content-Type to match the multipart/related MIME type
            var mimeContentType = mimeMessage.Body.ContentType.MimeType
                + "; "
                + mimeMessage.Body.ContentType.Parameters;
            content.Headers.ContentType = MediaTypeHeaderValue.Parse(
                mimeMessage.Body.ContentType.ToString());
            request.Content = content;

            // Send
            using var response = await HttpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            // Parse response
            return await As4ResponseParser.ParseAsync(response, options, decryptionCert, cancellationToken);
        }
        catch (Exception ex)
        {
            return ErrorHandler.Handle(ex, options.ThrowErrorOnFailure, options.ErrorMessageOnFailure);
        }
    }

    // -------------------------------------------------------------------------
    // Certificate helpers
    // -------------------------------------------------------------------------

    private static X509Certificate2 LoadPfxCertificate(string path, string password, string role)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        if (!File.Exists(path))
            throw new FileNotFoundException($"The {role} certificate file was not found: {path}", path);

        return new X509Certificate2(path, password, X509KeyStorageFlags.Exportable);
    }

    private static X509Certificate2 LoadPublicCertificate(string path, string role)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        if (!File.Exists(path))
            throw new FileNotFoundException($"The {role} certificate file was not found: {path}", path);

        return new X509Certificate2(path);
    }
}
