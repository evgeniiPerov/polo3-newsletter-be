namespace Polo3.Newsletter.Web.Services;

public sealed class NewsletterOptions
{
    public const string SectionName = "Newsletter";

    /// <summary>Public address of the site, used to build the links in the confirmation email.</summary>
    public Uri BaseUrl { get; set; } = new("http://localhost:5080");
}
