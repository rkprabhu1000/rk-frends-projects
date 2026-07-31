namespace Frends.SAS.Read.Definitions;

/// <summary>
/// Describes a single column in the source .sas7bdat file.
/// </summary>
public class ColumnInfo
{
    /// <summary>
    /// Column name as declared in the SAS dataset.
    /// </summary>
    /// <example>BIRTH_DATE</example>
    public string Name { get; set; }

    /// <summary>
    /// Optional descriptive label assigned to the column in SAS. Empty when not set.
    /// </summary>
    /// <example>Date of birth</example>
    public string Label { get; set; }

    /// <summary>
    /// Underlying SAS storage type: Numeric or Character.
    /// </summary>
    /// <example>Numeric</example>
    public string SasType { get; set; }

    /// <summary>
    /// SAS format string applied to the column, e.g. DATE9., DATETIME20., COMMA12.2.
    /// Used to detect Date/DateTime/Time semantics on Numeric columns. Empty when unset.
    /// </summary>
    /// <example>DATE9.</example>
    public string Format { get; set; }

    /// <summary>
    /// Column byte length as stored in the source file.
    /// </summary>
    /// <example>8</example>
    public int Length { get; set; }
}
