using System;

namespace Frends.URLDownload.Dalux.Helpers;

internal static class ContentDispositionHelper
{
    // RFC 5987 extended value: filename*=UTF-8''<percent-encoded-name>
    private const string Utf8Prefix = "UTF-8''";

    internal static string ExtractFileName(string contentDisposition)
    {
        var idx = contentDisposition.IndexOf(Utf8Prefix, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
            return Uri.UnescapeDataString(contentDisposition[(idx + Utf8Prefix.Length)..].Trim());
        return "downloaded_file.pdf";
    }
}
