namespace Polo3.Newsletter.Web.Services;

/// <summary>Outcome of a service call with the message shown to the user.</summary>
public sealed record NewsletterResult(NewsletterResultCode Code, string Message)
{
    public bool IsSuccess => Code is not (NewsletterResultCode.InvalidInput or NewsletterResultCode.TokenInvalid or NewsletterResultCode.ServiceUnavailable);
}
