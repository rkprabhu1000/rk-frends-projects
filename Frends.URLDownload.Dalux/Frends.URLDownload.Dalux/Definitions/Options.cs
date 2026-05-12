using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Frends.URLDownload.Dalux.Definitions;

/// <summary>
/// Additional options controlling the Dalux Download task behaviour.
/// </summary>
public class Options
{
    /// <summary>
    /// When true, Firefox runs without a visible browser window.
    /// Always set to true on server/agent environments.
    /// </summary>
    /// <example>true</example>
    [Display(Name = "Headless")]
    [DefaultValue(true)]
    public bool Headless { get; set; } = true;

    /// <summary>
    /// Maximum milliseconds to wait for each browser operation (page load, selector, response interception).
    /// Increase if the Dalux login page is slow to respond.
    /// </summary>
    /// <example>30000</example>
    [Display(Name = "Timeout (ms)")]
    [DefaultValue(30000)]
    public int TimeoutMs { get; set; } = 30000;

    /// <summary>
    /// When true, automatically runs "playwright install firefox" if the Firefox binary
    /// is not present on the agent machine. Requires internet access on first run.
    /// Safe to leave enabled; subsequent runs skip the download when the binary is already cached.
    /// </summary>
    /// <example>true</example>
    [Display(Name = "Install Browser If Missing")]
    [DefaultValue(true)]
    public bool InstallBrowserIfMissing { get; set; } = true;

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
    /// Overrides the error message returned or thrown on failure.
    /// When empty, the original exception message is used.
    /// </summary>
    /// <example>Dalux file download failed</example>
    [DisplayFormat(DataFormatString = "Text")]
    [DefaultValue("")]
    public string ErrorMessageOnFailure { get; set; } = string.Empty;
}
