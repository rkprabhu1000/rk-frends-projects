using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using MimeKit;

namespace Frends.AS4.PullRequest.Helpers;

/// <summary>
/// Parses an inbound AS4 Multipart/Related request body to extract the eb:PullRequest signal.
/// </summary>
internal static class PullRequestParser
{
    private const string NsEbms = "http://docs.oasis-open.org/ebxml-msg/ebms/v3.0/ns/core/20070523/";

    /// <summary>
    /// Parses the raw MIME request body and returns the MPC and PullRequest MessageId.
    /// </summary>
    internal static (string mpc, string messageId) Parse(string requestBody, string contentTypeHeader)
    {
        var soapDoc = ExtractSoapDocument(requestBody, contentTypeHeader);
        return ExtractPullRequestFields(soapDoc);
    }

    internal static XmlDocument ExtractSoapDocument(string requestBody, string contentTypeHeader)
    {
        // Reconstruct a parseable MIME entity
        var raw = Encoding.UTF8.GetBytes($"Content-Type: {contentTypeHeader}\r\n\r\n{requestBody}");

        MimeEntity entity;
        using (var ms = new MemoryStream(raw))
            entity = MimeEntity.Load(ms);

        XmlDocument soapDoc;

        if (entity is Multipart multipart)
        {
            var soapPart = multipart.OfType<MimePart>().FirstOrDefault(p =>
                p.ContentType.MimeType.Contains("soap", StringComparison.OrdinalIgnoreCase) ||
                p.ContentType.MimeType.Contains("xml", StringComparison.OrdinalIgnoreCase))
                ?? throw new FormatException("No SOAP part found in the Multipart/Related message.");

            using var ms = new MemoryStream();
            soapPart.Content.DecodeTo(ms);
            soapDoc = LoadXml(Encoding.UTF8.GetString(ms.ToArray()));
        }
        else if (entity is MimePart part)
        {
            // Single-part body (plain SOAP without attachment — valid for PullRequest)
            using var ms = new MemoryStream();
            part.Content.DecodeTo(ms);
            soapDoc = LoadXml(Encoding.UTF8.GetString(ms.ToArray()));
        }
        else
        {
            // Try parsing the body directly as XML
            soapDoc = LoadXml(requestBody);
        }

        return soapDoc;
    }

    internal static (string mpc, string messageId) ExtractPullRequestFields(XmlDocument soapDoc)
    {
        var ns = new XmlNamespaceManager(soapDoc.NameTable);
        ns.AddNamespace("eb", NsEbms);

        var pullNode = soapDoc.SelectSingleNode("//eb:PullRequest", ns)
            ?? throw new FormatException(
                "No eb:PullRequest element found in the SOAP header. " +
                "Ensure the request is an AS4 Pull signal and not a UserMessage.");

        var mpc = ((XmlElement)pullNode).GetAttribute("mpc");
        var messageId = soapDoc.SelectSingleNode(
            "//eb:SignalMessage/eb:MessageInfo/eb:MessageId", ns)?.InnerText?.Trim();

        return (mpc, messageId);
    }

    private static XmlDocument LoadXml(string xml)
    {
        var doc = new XmlDocument { PreserveWhitespace = false };
        doc.LoadXml(xml);
        return doc;
    }
}
