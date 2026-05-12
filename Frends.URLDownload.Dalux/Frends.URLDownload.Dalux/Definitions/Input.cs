using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Frends.URLDownload.Dalux.Definitions;

/// <summary>
/// Input parameters for the Dalux DownloadFile task.
/// </summary>
public class Input
{
    /// <summary>
    /// The Dalux login page URL, including the returnUrl parameter that redirects
    /// to the file download after successful authentication.
    /// </summary>
    /// <example>https://node1.build.dalux.com/client/login?returnUrl=%2F386464288623034369%2Fredirection%3Floginandredirecttoservice%3Dhttps%3A...</example>
    [Display(Name = "Calling URL")]
    [DisplayFormat(DataFormatString = "Text")]
    [DefaultValue("")]
    public string CallingUrl { get; set; }

    /// <summary>
    /// The direct URL of the file to download after authentication.
    /// </summary>
    /// <example>https://node1.field.dalux.com/service/web/downloadfile/project-386464288623034369/areafilerevision-442383144335704067</example>
    [Display(Name = "File URL")]
    [DisplayFormat(DataFormatString = "Text")]
    [DefaultValue("")]
    public string FileUrl { get; set; }

    /// <summary>
    /// The Dalux account email address used to log in.
    /// </summary>
    /// <example>user@example.com</example>
    [DisplayFormat(DataFormatString = "Text")]
    [DefaultValue("")]
    public string Email { get; set; }

    /// <summary>
    /// The Dalux account password.
    /// </summary>
    /// <example>YourDaluxPassword</example>
    [PasswordPropertyText(true)]
    [DefaultValue("")]
    public string Password { get; set; }
}
