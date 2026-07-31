namespace Frends.SAS.Read.Definitions;

/// <summary>
/// Specifies where the .sas7bdat file content is read from.
/// </summary>
public enum SasInputType
{
    /// <summary>
    /// Read directly from a file path on the agent's filesystem. Streamed, so large
    /// files do not need to be loaded into memory up front.
    /// </summary>
    File,

    /// <summary>
    /// Read from an in-memory byte array already present in the process flow (e.g.
    /// from Files.Read or an HTTP download).
    /// </summary>
    Bytes,
}
