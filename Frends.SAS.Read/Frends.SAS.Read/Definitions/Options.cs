using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Frends.SAS.Read.Definitions;

/// <summary>
/// Additional options controlling the SAS Read task behaviour.
/// </summary>
public class Options
{
    /// <summary>
    /// When true, the result includes a Columns array describing each column's name,
    /// SAS storage type, format, label and length. Use this to build schema-aware
    /// transformations of Rows further down the process.
    /// </summary>
    /// <example>true</example>
    [Display(Name = "Include Column Metadata")]
    [DefaultValue(true)]
    public bool IncludeColumnMetadata { get; set; } = true;

    /// <summary>
    /// Controls how SAS Date, DateTime and Time columns are rendered. Iso8601String
    /// converts recognized date/datetime/time columns to ISO 8601 strings.
    /// RawSasNumericValue leaves them as the raw SAS numeric value for callers that
    /// want to apply their own conversion. Columns using a format the reader doesn't
    /// recognize as temporal are always returned as their raw numeric value.
    /// </summary>
    /// <example>SasDateOutputFormat.Iso8601String</example>
    [Display(Name = "Date Output Format")]
    [DefaultValue(SasDateOutputFormat.Iso8601String)]
    public SasDateOutputFormat DateOutputFormat { get; set; } = SasDateOutputFormat.Iso8601String;

    /// <summary>
    /// Maximum number of data rows to read. Leave at 0 to read all rows. Use this to
    /// preview large files without reading the entire dataset.
    /// </summary>
    /// <example>0</example>
    [Display(Name = "Max Rows")]
    [DefaultValue(0)]
    public int MaxRows { get; set; } = 0;

    /// <summary>
    /// Overrides the character encoding used to decode character columns. Leave empty
    /// to use the encoding declared in the .sas7bdat file header.
    /// </summary>
    /// <example>windows-1252</example>
    [DisplayFormat(DataFormatString = "Text")]
    [DefaultValue("")]
    public string EncodingOverride { get; set; } = string.Empty;

    /// <summary>
    /// When true, any exception during task execution is re-thrown and propagates to
    /// the Frends process. When false, the exception is captured in Result.Error and
    /// Result.Success is set to false.
    /// </summary>
    /// <example>true</example>
    [Display(Name = "Throw Error On Failure")]
    [DefaultValue(true)]
    public bool ThrowErrorOnFailure { get; set; } = true;

    /// <summary>
    /// Overrides the error message returned or thrown on failure. When empty, the
    /// original exception message is used.
    /// </summary>
    /// <example>SAS file read failed</example>
    [DisplayFormat(DataFormatString = "Text")]
    [DefaultValue("")]
    public string ErrorMessageOnFailure { get; set; } = string.Empty;
}
