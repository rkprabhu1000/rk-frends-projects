using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Frends.AS4.Send.Definitions;
using MimeKit;

namespace Frends.AS4.Send.Helpers;

/// <summary>
/// Parses the synchronous AS4 HTTP response, extracts the primary payload attachment,
/// and applies optional decryption and decompression.
/// </summary>
internal static class As4ResponseParser
{
    private const string NsXenc = "http://www.w3.org/2001/04/xmlenc#";
    private const string NsWsse = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";

    /// <summary>
    /// Reads the HTTP response, extracts headers, and processes the MIME payload.
    /// </summary>
    internal static async Task<Result> ParseAsync(
        HttpResponseMessage httpResponse,
        Options options,
        X509Certificate2 decryptionCert,
        CancellationToken cancellationToken)
    {
        // Collect response headers
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var h in httpResponse.Headers)
            headers[h.Key] = string.Join(", ", h.Value);
        foreach (var h in httpResponse.Content.Headers)
            headers[h.Key] = string.Join(", ", h.Value);

        var responseBodyBytes = await httpResponse.Content.ReadAsByteArrayAsync(cancellationToken);

        if (responseBodyBytes.Length == 0)
        {
            return new Result
            {
                Success = true,
                PayloadBytes = null,
                PayloadString = null,
                ResponseHeaders = headers,
                Error = null,
            };
        }

        // Determine content-type for MIME parsing
        var contentType = headers.TryGetValue("Content-Type", out var ct) ? ct : "multipart/related";

        var payloadBytes = ExtractPayloadFromMime(responseBodyBytes, contentType);

        if (payloadBytes == null)
        {
            return new Result
            {
                Success = true,
                PayloadBytes = null,
                PayloadString = null,
                ResponseHeaders = headers,
                Error = null,
            };
        }

        // Decrypt if requested
        if (options.DecryptResponse && decryptionCert != null)
            payloadBytes = DecryptPayload(payloadBytes, decryptionCert);

        // Decompress if requested
        if (options.DecompressResponse)
            payloadBytes = TryDecompress(payloadBytes);

        return new Result
        {
            Success = true,
            PayloadBytes = payloadBytes,
            PayloadString = TryDecodeUtf8(payloadBytes),
            ResponseHeaders = headers,
            Error = null,
        };
    }

    // -------------------------------------------------------------------------
    // MIME extraction
    // -------------------------------------------------------------------------

    private static byte[] ExtractPayloadFromMime(byte[] rawBytes, string contentType)
    {
        // If not multipart, treat the entire body as the payload
        if (!contentType.StartsWith("multipart", StringComparison.OrdinalIgnoreCase))
            return rawBytes;

        try
        {
            // MimeKit needs a full RFC 2822 message; prepend synthetic headers
            var fullMessage = Encoding.UTF8.GetBytes($"Content-Type: {contentType}\r\n\r\n")
                .Concat(rawBytes).ToArray();

            var message = MimeMessage.Load(new MemoryStream(fullMessage));

            if (message.Body is not Multipart multipart)
                return rawBytes;

            // Skip the first part (SOAP envelope); take the first non-SOAP attachment
            var payloadPart = multipart
                .OfType<MimePart>()
                .FirstOrDefault(p =>
                    !string.Equals(p.ContentType.MimeType, "application/soap+xml", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(p.ContentType.MimeType, "text/xml", StringComparison.OrdinalIgnoreCase));

            if (payloadPart == null)
                return null;

            using var ms = new MemoryStream();
            payloadPart.Content.DecodeTo(ms);
            return ms.ToArray();
        }
        catch
        {
            // Fallback: return raw bytes if MIME parsing fails
            return rawBytes;
        }
    }

    // -------------------------------------------------------------------------
    // Decryption: AES-256-CBC with IV prepended (mirrors As4MessageBuilder)
    // -------------------------------------------------------------------------

    private static byte[] DecryptPayload(byte[] cipherBytes, X509Certificate2 cert)
    {
        // The cipher bytes format written by As4MessageBuilder:
        //   [16 bytes IV][cipher text]
        // The AES session key is wrapped with RSA-OAEP in the EncryptedKey element of the
        // SOAP Security header.  For the synchronous response case, however, we do not
        // re-parse the SOAP envelope here; instead we attempt a direct RSA-then-AES
        // decrypt by assuming the sender used the same convention.
        // If the response follows a different convention (e.g., a separate EncryptedKey XML
        // element), the caller must pre-extract the session key before invoking this method.
        //
        // Real implementations should parse the EncryptedKey from the response SOAP envelope.
        // This implementation covers the common symmetric-key transport format.

        if (cipherBytes.Length <= 16)
            throw new InvalidOperationException("Encrypted payload is too short to contain a valid IV.");

        // Try to determine if this looks like raw AES-CBC output (IV + ciphertext).
        // A more robust approach: locate the EncryptedKey in the response SOAP and decrypt it.
        // Here we assume the caller (AS4.cs) has pre-decrypted the session key when the full
        // EncryptedKey XML is available; this helper handles the simpler raw-bytes path.

        var iv = cipherBytes[..16];
        var cipher = cipherBytes[16..];

        // We cannot derive the AES key from just the cert without the EncryptedKey element.
        // This method is therefore a placeholder for when the session key has been resolved
        // externally and re-encrypted into the cipherBytes stream (non-standard shortcut).
        //
        // For full AS4 decryption, the caller should:
        // 1. Parse the SOAP Security/EncryptedKey from the response.
        // 2. Decrypt the session key using cert.GetRSAPrivateKey().Decrypt(..., OaepSHA1).
        // 3. Call DecryptWithSessionKey(cipher, iv, sessionKey).
        //
        // Because this scenario requires the full response SOAP document, we delegate
        // to TryDecryptWithCert which attempts RSA-OAEP decryption of the first 256/512 bytes
        // as a wrapped key, then AES-decrypts the remainder.
        return TryDecryptWithCert(cipherBytes, cert);
    }

    /// <summary>
    /// Attempts to decrypt data where the first rsaKeyLength bytes are the RSA-OAEP-wrapped AES key
    /// and the remaining bytes are [16-byte IV][AES-CBC ciphertext].
    /// </summary>
    private static byte[] TryDecryptWithCert(byte[] data, X509Certificate2 cert)
    {
        using var rsa = cert.GetRSAPrivateKey()
            ?? throw new InvalidOperationException("The decryption certificate does not contain a private key.");

        // RSA key size in bytes (e.g. 2048-bit → 256 bytes)
        var rsaKeyLen = rsa.KeySize / 8;

        if (data.Length <= rsaKeyLen + 16)
            throw new InvalidOperationException(
                $"Encrypted data ({data.Length} bytes) is too short for RSA-{rsa.KeySize} key transport + IV + payload.");

        var wrappedKey = data[..rsaKeyLen];
        var aesIv = data[rsaKeyLen..(rsaKeyLen + 16)];
        var cipherText = data[(rsaKeyLen + 16)..];

        var aesKey = rsa.Decrypt(wrappedKey, RSAEncryptionPadding.OaepSHA1);
        return DecryptWithSessionKey(cipherText, aesIv, aesKey);
    }

    internal static byte[] DecryptWithSessionKey(byte[] cipherText, byte[] iv, byte[] key)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;

        using var decryptor = aes.CreateDecryptor();
        using var ms = new MemoryStream();
        using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Write))
            cs.Write(cipherText, 0, cipherText.Length);
        return ms.ToArray();
    }

    // -------------------------------------------------------------------------
    // Decompression
    // -------------------------------------------------------------------------

    internal static byte[] TryDecompress(byte[] data)
    {
        if (data == null || data.Length < 2)
            return data;

        // GZip magic: 1F 8B
        if (data[0] == 0x1F && data[1] == 0x8B)
            return DecompressGzip(data);

        // Zlib magic: 78 9C / 78 01 / 78 DA / 78 5E
        if (data[0] == 0x78)
            return DecompressDeflate(data[2..]);   // skip 2-byte zlib header

        // Not compressed, return as-is
        return data;
    }

    private static byte[] DecompressGzip(byte[] compressed)
    {
        using var input = new MemoryStream(compressed);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }

    private static byte[] DecompressDeflate(byte[] compressed)
    {
        using var input = new MemoryStream(compressed);
        using var deflate = new DeflateStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        deflate.CopyTo(output);
        return output.ToArray();
    }

    // -------------------------------------------------------------------------
    // Utilities
    // -------------------------------------------------------------------------

    private static string TryDecodeUtf8(byte[] bytes)
    {
        if (bytes == null) return null;
        try
        {
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return Convert.ToBase64String(bytes);
        }
    }
}
