using System;
using System.Globalization;
using System.Linq;
using SasReader;

namespace Frends.SAS.Read.Helpers;

/// <summary>
/// Renders values returned by SasFileReaderImpl into JSON-friendly representations,
/// converting recognized SAS date/datetime/time columns (already materialized as
/// System.DateTime by the reader) to ISO 8601 strings. Also covers a small set of
/// day-precision SAS date formats that SasReader's own Is*Format helpers don't
/// recognize, so the reader leaves them as a raw numeric value instead of a DateTime -
/// these are converted here using SAS's standard epoch (days since 1960-01-01).
/// </summary>
internal static class SasTemporalHelper
{
    private static readonly DateTime SasEpoch = new(1960, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

    /// <summary>
    /// SAS date formats (day precision, no time component) that SasReader does not
    /// classify via SasTemporalFormatter.isDateFormat, verified against pandas'
    /// read_sas (which does recognize them) to confirm they use the standard SAS
    /// day-count epoch rather than some other storage scheme. Sourced by diffing
    /// SasReader's actual recognized-format tables against pandas' sas_date_formats
    /// list (itself taken from parso's SasFileConstants.java - the same upstream
    /// library SasReader is ported from, so this is a real coverage gap in the
    /// .NET port, not a difference in behaviour between the two ecosystems). Add to
    /// this list as further unrecognized formats are found in real files.
    /// </summary>
    private static readonly string[] AdditionalRecognizedDateFormats =
    {
        "MINGUO", // Taiwanese/ROC calendar
        "NENGO", // Japanese imperial era calendar
        "QTR", "QTRR", // Quarter
        "WEEKV", // Week
        "YYQ", "YYQC", "YYQD", "YYQP", "YYQS", "YYQN", // Year-quarter
        "YYQR", "YYQRC", "YYQRD", "YYQRP", "YYQRS", "YYQRN", // Year-quarter (4-digit year)
    };

    /// <summary>
    /// Converts a single raw value returned by the reader to a JSON-friendly value,
    /// rendering recognized date/datetime/time values as ISO 8601 strings.
    /// </summary>
    /// <param name="rawValue">The raw value returned by SasFileReaderImpl for this column/row.</param>
    /// <param name="sasFormatName">The column's SAS format name, used to classify date/datetime/time semantics.</param>
    /// <param name="convertDates">Whether date conversion should be attempted at all (false when the caller requested raw SAS numeric values).</param>
    /// <returns>An ISO 8601 string for recognized date/datetime/time values, otherwise the original value unchanged.</returns>
    public static object ToJsonValue(object rawValue, string sasFormatName, bool convertDates)
    {
        if (!convertDates)
            return rawValue;

        if (rawValue is DateTime dateTimeValue)
        {
            if (SasTemporalFormatter.isTimeFormat(sasFormatName))
                return dateTimeValue.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

            if (SasTemporalFormatter.isDateTimeFormat(sasFormatName))
                return dateTimeValue.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

            if (SasTemporalFormatter.isDateFormat(sasFormatName))
                return dateTimeValue.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            // The reader returned a DateTime for a format it doesn't classify via the
            // Is*Format helpers above (has been observed for a handful of niche SAS
            // formats) - fall back to a full timestamp rather than dropping precision.
            return dateTimeValue.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
        }

        if (rawValue is double numericValue &&
            sasFormatName != null &&
            AdditionalRecognizedDateFormats.Contains(sasFormatName, StringComparer.OrdinalIgnoreCase))
        {
            return SasEpoch.AddDays(numericValue).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        return rawValue;
    }
}
