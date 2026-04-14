using System;
using System.ComponentModel;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Frends.AS4.PullRequest.Definitions;
using Frends.AS4.PullRequest.Helpers;

namespace Frends.AS4.PullRequest;

/// <summary>
/// Task class for AS4 PullRequest operations.
/// </summary>
public static class AS4
{
    /// <summary>
    /// Handles an inbound AS4 eb:PullRequest signal received via a Frends HTTP trigger.
    /// Parses the MPC from the PullRequest, then either:
    /// (a) builds and returns a signed, encrypted UserMessage containing the provided payload, or
    /// (b) returns an EBMS:0006 EmptyMessagePartitionChannel warning signal when no payload is provided.
    /// The returned ResponseBody and ResponseContentType are intended to be used directly
    /// as the synchronous HTTP response body and Content-Type header of the Frends HTTP trigger,
    /// completing the back-channel exchange required by the AS4 Pull pattern.
    /// [Documentation](https://tasks.frends.com/tasks/CHANNEL/Frends.AS4.PullRequest)
    /// </summary>
    /// <param name="input">
    /// The raw HTTP trigger body and Content-Type header containing the eb:PullRequest signal,
    /// plus the optional payload file path and routing/certificate parameters for the response.
    /// </param>
    /// <param name="options">
    /// Behavioural flags: SignResponse, EncryptResponse, and ThrowErrorOnFailure.
    /// </param>
    /// <param name="cancellationToken">A cancellation token provided by the Frends Platform.</param>
    /// <returns>
    /// object {
    ///   bool Success,
    ///   string ResponseBody,
    ///   string ResponseContentType,
    ///   bool IsEmptyQueue,
    ///   string RequestedMpc,
    ///   string PullRequestMessageId,
    ///   object Error { string Message, Exception AdditionalInfo }
    /// }
    /// </returns>
    public static Task<Result> PullRequest(
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

            // Parse the inbound PullRequest to extract MPC and MessageId
            var (requestedMpc, pullRequestMessageId) = PullRequestParser.Parse(
                input.RequestBody, input.ContentTypeHeader);

            cancellationToken.ThrowIfCancellationRequested();

            // No payload → return EBMS:0006 empty queue signal
            if (string.IsNullOrWhiteSpace(input.PayloadFilePath))
            {
                var (emptyBody, emptyCt) = EmptyQueueSignalBuilder.Build(requestedMpc, pullRequestMessageId);
                return Task.FromResult(new Result
                {
                    Success = true,
                    IsEmptyQueue = true,
                    RequestedMpc = requestedMpc,
                    PullRequestMessageId = pullRequestMessageId,
                    ResponseBody = emptyBody,
                    ResponseContentType = emptyCt,
                    Error = null,
                });
            }

            if (!File.Exists(input.PayloadFilePath))
                throw new FileNotFoundException(
                    $"Payload file not found: {input.PayloadFilePath}", input.PayloadFilePath);

            var payloadBytes = File.ReadAllBytes(input.PayloadFilePath);
            var payloadFileName = Path.GetFileName(input.PayloadFilePath);

            cancellationToken.ThrowIfCancellationRequested();

            // Load certificates
            var senderCert = LoadPfxCertificate(
                input.SenderCertificatePath, input.SenderCertificatePassword, "sender");
            var recipientCert = LoadPublicCertificate(input.RecipientCertificatePath, "recipient");

            // Build the UserMessage response
            var (mimeBody, contentType) = UserMessageBuilder.Build(
                payloadBytes,
                payloadFileName,
                input,
                options,
                pullRequestMessageId,
                senderCert,
                recipientCert);

            return Task.FromResult(new Result
            {
                Success = true,
                IsEmptyQueue = false,
                RequestedMpc = requestedMpc,
                PullRequestMessageId = pullRequestMessageId,
                ResponseBody = mimeBody,
                ResponseContentType = contentType,
                Error = null,
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(
                ErrorHandler.Handle(ex, options.ThrowErrorOnFailure, options.ErrorMessageOnFailure));
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
