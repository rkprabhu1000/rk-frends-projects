namespace Frends.SAS.Read.Definitions;

/// <summary>
/// Controls how SAS Date, DateTime and Time columns are rendered in the output.
/// </summary>
public enum SasDateOutputFormat
{
    /// <summary>
    /// Convert recognized date/datetime/time columns to ISO 8601 strings.
    /// </summary>
    Iso8601String,

    /// <summary>
    /// Leave the raw SAS numeric value unconverted (days since 1960-01-01 for dates,
    /// seconds since 1960-01-01T00:00:00 for datetimes/times).
    /// </summary>
    RawSasNumericValue,
}
