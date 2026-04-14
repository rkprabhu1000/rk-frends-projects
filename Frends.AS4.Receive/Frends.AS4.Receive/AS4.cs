using System;
using System.ComponentModel;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Frends.AS4.Receive.Definitions;
using Frends.AS4.Receive.Helpers;

namespace Frends.AS4.Receive;

/// <summary>
/// Task class for AS4 Receive operations.
/// </summary>
public static class AS4
{
    /// <summary>
    /// Processes an inbound AS4 message received via a Frends HTTP trigger.
    /// Parses the Multipart/Related SOAP-with-Attachments body, extracts the eb:Messaging
    /// routing headers, optionally verifies the WS-Security RSA-SHA256 signature,
    /// decrypts the payload attachment using the receiver's private certificate,
    /// and decompresses it if GZip or Deflate compression is detected.
    /// [Documentation](https://tasks.frends.com/tasks/CHANNEL/Frends.AS4.Receive)
    /// </summary>
    /// <param name="input">
    /// The raw HTTP request body and Content-Type header from the Frends HTTP trigger,
    /// plus certificate paths for decryption and signature verification.
    /// </param>
    /// <param name="options">
    /// Behavioural flags: VerifySignature, DecryptPayload, DecompressPayload,
    /// and ThrowErrorOnFailure.
    /// </param>
    /// <param name="cancellationToken">A cancellation token provided by the Frends Platform.</param>
    /// <returns>
    /// object {
    ///   bool Success,
    ///   string PayloadString,
    ///   byte[] PayloadBytes,
    ///   string MessageId,
    ///   string SenderPartyId,
    ///   string RecipientPartyId,
    ///   string Service,
    ///   string Action,
    ///   string ConversationId,
    ///   string AgreementRef,
    ///   bool SignatureVerified,
    ///   object Error { string Message, Exception AdditionalInfo }
    /// }
    /// </returns>
    public static Task<Result> Receive(
        [PropertyTab] Input input,
        [PropertyTab] Options options,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(input.RequestBody))
                throw new ArgumentException("RequestBody cannot be empty.", nameof(input));

            if (string.IsNullOrWhiteSpace(input.ContentTypeHeader))
                throw new ArgumentException("ContentTypeHeader cannot be empty.", nameof(input));

            // Load certificates
            var receiverCert = LoadPfxCertificate(
                input.ReceiverCertificatePath,
                input.ReceiverCertificatePassword,
                "receiver");

            var senderCert = LoadPublicCertificate(input.SenderCertificatePath, "sender");

            cancellationToken.ThrowIfCancellationRequested();

            // Parse the MIME message and optionally decrypt / decompress the attachment
            var parsed = As4MessageParser.Parse(
                input.RequestBody,
                input.ContentTypeHeader,
                receiverCert,
                options.DecryptPayload,
                options.DecompressPayload);

            cancellationToken.ThrowIfCancellationRequested();

            // Verify WS-Security signature
            var signatureVerified = false;
            if (options.VerifySignature)
            {
                As4SignatureVerifier.Verify(parsed.SoapDocument, parsed.AttachmentBytes, senderCert);
                signatureVerified = true;
            }

            var meta = parsed.Metadata;

            return Task.FromResult(new Result
            {
                Success = true,
                PayloadBytes = parsed.AttachmentBytes,
                PayloadString = As4MessageParser.TryDecodeUtf8(parsed.AttachmentBytes),
                MessageId = meta.MessageId,
                SenderPartyId = meta.SenderPartyId,
                RecipientPartyId = meta.RecipientPartyId,
                Service = meta.Service,
                Action = meta.Action,
                ConversationId = meta.ConversationId,
                AgreementRef = meta.AgreementRef,
                SignatureVerified = signatureVerified,
                Error = null,
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(ErrorHandler.Handle(ex, options.ThrowErrorOnFailure, options.ErrorMessageOnFailure));
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
