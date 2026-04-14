using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml;

namespace Frends.AS4.Receive.Helpers;

/// <summary>
/// Verifies the WS-Security XML Digital Signature on an inbound AS4 message.
/// Checks the RSA-SHA256 signature over the eb:Messaging element and the payload attachment digest.
/// </summary>
internal static class As4SignatureVerifier
{
    private const string NsWsse = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";
    private const string NsDs = "http://www.w3.org/2000/09/xmldsig#";
    private const string NsEbms = "http://docs.oasis-open.org/ebxml-msg/ebms/v3.0/ns/core/20070523/";
    private const string AlgRsaSha256 = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256";
    private const string AlgSha256 = "http://www.w3.org/2001/04/xmlenc#sha256";
    private const string AlgExcC14N = "http://www.w3.org/2001/10/xml-exc-c14n#";

    /// <summary>
    /// Verifies the ds:Signature in the wsse:Security header of <paramref name="soapDoc"/>.
    /// </summary>
    /// <param name="soapDoc">The parsed SOAP envelope.</param>
    /// <param name="attachmentBytes">Raw (post-decryption) attachment bytes used for digest verification.</param>
    /// <param name="trustedSenderCert">
    /// Optional trusted sender certificate. When null the certificate is taken from the
    /// wsse:BinarySecurityToken in the message itself (the signature is still mathematically
    /// verified but the trust anchor is not checked against an external store).
    /// </param>
    /// <exception cref="InvalidOperationException">Thrown when no Signature element is found.</exception>
    /// <exception cref="CryptographicException">Thrown when the signature or a digest is invalid.</exception>
    internal static void Verify(XmlDocument soapDoc, byte[] attachmentBytes, X509Certificate2 trustedSenderCert)
    {
        var ns = BuildNsManager(soapDoc);

        // Locate the ds:Signature element
        var signatureNode = (XmlElement)soapDoc.SelectSingleNode("//ds:Signature", ns)
            ?? throw new InvalidOperationException("No ds:Signature element found in the WS-Security header.");

        // Resolve the signing certificate
        var signingCert = trustedSenderCert ?? ExtractCertFromBst(soapDoc, ns);

        // Verify each ds:Reference
        var references = signatureNode.SelectNodes("ds:SignedInfo/ds:Reference", ns);
        if (references == null || references.Count == 0)
            throw new InvalidOperationException("ds:SignedInfo contains no ds:Reference elements.");

        foreach (XmlElement reference in references)
        {
            VerifyReference(reference, soapDoc, attachmentBytes, ns);
        }

        // Verify the signature value over the canonicalized SignedInfo
        var signedInfoNode = (XmlElement)signatureNode.SelectSingleNode("ds:SignedInfo", ns)
            ?? throw new InvalidOperationException("ds:SignedInfo element not found.");

        var c14nSignedInfo = CanonicalizNodeExclusive(signedInfoNode);

        var sigValueNode = signatureNode.SelectSingleNode("ds:SignatureValue", ns)
            ?? throw new InvalidOperationException("ds:SignatureValue element not found.");
        var signatureBytes = Convert.FromBase64String(sigValueNode.InnerText.Trim());

        using var rsa = signingCert.GetRSAPublicKey()
            ?? throw new InvalidOperationException("The signing certificate does not contain an RSA public key.");

        var valid = rsa.VerifyData(c14nSignedInfo, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        if (!valid)
            throw new CryptographicException("AS4 WS-Security signature verification failed: SignatureValue is invalid.");
    }

    // -------------------------------------------------------------------------
    // Reference verification
    // -------------------------------------------------------------------------

    private static void VerifyReference(
        XmlElement reference,
        XmlDocument soapDoc,
        byte[] attachmentBytes,
        XmlNamespaceManager ns)
    {
        var uri = reference.GetAttribute("URI");
        var expectedDigestBase64 = reference.SelectSingleNode("ds:DigestValue", ns)?.InnerText?.Trim()
            ?? throw new InvalidOperationException($"ds:DigestValue missing for reference URI '{uri}'.");
        var expectedDigest = Convert.FromBase64String(expectedDigestBase64);

        byte[] actualDigest;

        if (uri.StartsWith("cid:", StringComparison.OrdinalIgnoreCase))
        {
            // Attachment reference — digest covers raw attachment bytes
            if (attachmentBytes == null)
                throw new InvalidOperationException($"Signature references attachment '{uri}' but no attachment was found.");
            actualDigest = SHA256.HashData(attachmentBytes);
        }
        else if (uri.StartsWith("#", StringComparison.Ordinal))
        {
            // Element reference — find element by wsu:Id, canonicalize, digest
            var id = uri[1..];
            var element = FindElementById(soapDoc, id)
                ?? throw new InvalidOperationException($"Element with wsu:Id='{id}' not found in SOAP document.");
            var c14nBytes = CanonicalizNodeExclusive(element);
            actualDigest = SHA256.HashData(c14nBytes);
        }
        else
        {
            throw new NotSupportedException($"Unsupported ds:Reference URI scheme: '{uri}'.");
        }

        if (!CryptographicOperations.FixedTimeEquals(actualDigest, expectedDigest))
            throw new CryptographicException(
                $"AS4 signature digest mismatch for reference '{uri}'. " +
                $"Expected: {Convert.ToBase64String(expectedDigest)}, " +
                $"Actual: {Convert.ToBase64String(actualDigest)}");
    }

    // -------------------------------------------------------------------------
    // Certificate extraction from BinarySecurityToken
    // -------------------------------------------------------------------------

    private static X509Certificate2 ExtractCertFromBst(XmlDocument soapDoc, XmlNamespaceManager ns)
    {
        var bstNode = soapDoc.SelectSingleNode("//wsse:BinarySecurityToken", ns)
            ?? throw new InvalidOperationException(
                "No wsse:BinarySecurityToken found and no trusted sender certificate was provided.");

        var certBytes = Convert.FromBase64String(bstNode.InnerText.Trim());
        return new X509Certificate2(certBytes);
    }

    // -------------------------------------------------------------------------
    // Utilities
    // -------------------------------------------------------------------------

    private static byte[] CanonicalizNodeExclusive(XmlNode node)
    {
        var tempDoc = new XmlDocument { PreserveWhitespace = false };
        tempDoc.AppendChild(tempDoc.ImportNode(node, true));

        var transform = new XmlDsigExcC14NTransform();
        transform.LoadInput(tempDoc);
        using var stream = (Stream)transform.GetOutput(typeof(Stream));
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    private static XmlElement FindElementById(XmlDocument doc, string id)
    {
        // Try wsu:Id attribute first, then plain id attribute
        var ns = new XmlNamespaceManager(doc.NameTable);
        ns.AddNamespace("wsu", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd");

        return (XmlElement)(
            doc.SelectSingleNode($"//*[@wsu:Id='{id}']", ns) ??
            doc.SelectSingleNode($"//*[@Id='{id}']") ??
            doc.SelectSingleNode($"//*[@id='{id}']"));
    }

    private static XmlNamespaceManager BuildNsManager(XmlDocument doc)
    {
        var ns = new XmlNamespaceManager(doc.NameTable);
        ns.AddNamespace("wsse", NsWsse);
        ns.AddNamespace("ds", NsDs);
        ns.AddNamespace("eb", NsEbms);
        return ns;
    }
}
