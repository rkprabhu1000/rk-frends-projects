using System;
using System.Text;

namespace Frends.AS4.PullRequest.Helpers;

/// <summary>
/// Builds an EBMS:0006 EmptyMessagePartitionChannel error signal response.
/// Returned when no message is available on the requested MPC.
/// Per the AS4 / ebMS 3.0 spec this is a Warning-severity signal, NOT an HTTP error.
/// The HTTP status code remains 200 OK.
/// </summary>
internal static class EmptyQueueSignalBuilder
{
    private const string NsSoap12 = "http://www.w3.org/2003/05/soap-envelope";
    private const string NsEbms = "http://docs.oasis-open.org/ebxml-msg/ebms/v3.0/ns/core/20070523/";

    /// <summary>
    /// Builds the SOAP error signal and wraps it in a single-part MIME body.
    /// Returns the MIME body string and Content-Type header value.
    /// </summary>
    internal static (string mimeBody, string contentType) Build(
        string requestedMpc,
        string refToMessageId)
    {
        var errorMessageId = $"{Guid.NewGuid()}@frends.as4";
        var timestamp = DateTime.UtcNow.ToString("o");

        var refToXml = !string.IsNullOrWhiteSpace(refToMessageId)
            ? $"<eb:RefToMessageId>{refToMessageId}</eb:RefToMessageId>"
            : string.Empty;

        var mpcInfo = !string.IsNullOrWhiteSpace(requestedMpc)
            ? $" (MPC: {requestedMpc})"
            : string.Empty;

        var soapXml = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<S12:Envelope xmlns:S12=""{NsSoap12}"" xmlns:eb=""{NsEbms}"">
  <S12:Header>
    <eb:Messaging S12:mustUnderstand=""true"">
      <eb:SignalMessage>
        <eb:MessageInfo>
          <eb:Timestamp>{timestamp}</eb:Timestamp>
          <eb:MessageId>{errorMessageId}</eb:MessageId>
          {refToXml}
        </eb:MessageInfo>
        <eb:Error errorCode=""EBMS:0006""
                  severity=""warning""
                  origin=""ebMS""
                  category=""Communication""
                  shortDescription=""EmptyMessagePartitionChannel""
                  refToMessageInError=""{refToMessageId ?? string.Empty}"">
          <eb:Description xml:lang=""en"">There is no message available for pulling from this MPC at this moment{mpcInfo}.</eb:Description>
        </eb:Error>
      </eb:SignalMessage>
    </eb:Messaging>
  </S12:Header>
  <S12:Body/>
</S12:Envelope>";

        var boundary = $"MIMEBoundary_{Guid.NewGuid():N}";

        var mimeBody =
            $"--{boundary}\r\n" +
            $"Content-Type: application/soap+xml; charset=UTF-8\r\n" +
            $"Content-Transfer-Encoding: 8bit\r\n\r\n" +
            $"{soapXml}\r\n" +
            $"--{boundary}--";

        var contentType =
            $"multipart/related; type=\"application/soap+xml\"; " +
            $"start=\"<soap-envelope@frends>\"; boundary=\"{boundary}\"";

        return (mimeBody, contentType);
    }
}
