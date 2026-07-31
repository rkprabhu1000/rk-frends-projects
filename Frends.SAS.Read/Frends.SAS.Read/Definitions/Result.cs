using System.Collections.Generic;

namespace Frends.SAS.Read.Definitions;

/// <summary>
/// Result returned by the SAS Read task.
/// </summary>
public class Result
{
    /// <summary>
    /// Indicates whether the task completed successfully.
    /// </summary>
    /// <example>true</example>
    public bool Success { get; set; }

    /// <summary>
    /// Column metadata for the source dataset. Null when Options.IncludeColumnMetadata
    /// is false. Use this to drive schema-aware transformations of Rows.
    /// </summary>
    /// <example>object[] { object { string Name, string Label, string SasType, string Format, int Length } }</example>
    public List<ColumnInfo> Columns { get; set; }

    /// <summary>
    /// Number of data rows returned in Rows.
    /// </summary>
    /// <example>15000</example>
    public int RowCount { get; set; }

    /// <summary>
    /// Data rows, each as a dictionary keyed by column name. Values are strings,
    /// numbers or null so the result can be consumed directly as JSON.
    /// </summary>
    /// <example>object[] { object { string BIRTH_DATE, double SALARY } }</example>
    public List<Dictionary<string, object>> Rows { get; set; }

    /// <summary>
    /// Error information populated when Success is false. Null on successful completion.
    /// </summary>
    /// <example>object { string Message, Exception AdditionalInfo }</example>
    public Error Error { get; set; }
}
