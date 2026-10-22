using System.Buffers.Text;

namespace Polo3.Newsletter.Web;

/// <summary>Single source for the input limits; they match the column sizes in db/NewsletterSubscription.sql.</summary>
public static class NewsletterLimits
{
    public const int MaxEmailLength = 254;
    public const int MaxNameLength = 100;
    public const int TokenByteLength = 32;

    /// <summary>Base64Url length of <see cref="TokenByteLength"/> bytes without padding.</summary>
    public static readonly int TokenLength = Base64Url.GetEncodedLength(TokenByteLength);
}
