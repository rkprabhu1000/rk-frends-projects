using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Frends.SAS.Read.Definitions;

/// <summary>
/// Input parameters for the SAS Read task.
/// </summary>
public class Input
{
    /// <summary>
    /// Where the .sas7bdat file comes from. File reads directly from disk on the agent
    /// and streams the data without loading the whole file into memory. Bytes accepts
    /// file content already in the process flow but holds the entire file in memory.
    /// </summary>
    /// <example>SasInputType.File</example>
    [Display(Name = "Source Type")]
    [DefaultValue(SasInputType.File)]
    public SasInputType SourceType { get; set; } = SasInputType.File;

    /// <summary>
    /// Absolute path to the .sas7bdat file on the agent's filesystem.
    /// Required when SourceType is File.
    /// </summary>
    /// <example>C:\data\patients.sas7bdat</example>
    [UIHint(nameof(SourceType), "", SasInputType.File)]
    [DisplayFormat(DataFormatString = "Text")]
    [DefaultValue("")]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Raw .sas7bdat file content. Required when SourceType is Bytes.
    /// </summary>
    /// <example>byte[] file content</example>
    [UIHint(nameof(SourceType), "", SasInputType.Bytes)]
    [DefaultValue(null)]
    public byte[] Data { get; set; }
}
