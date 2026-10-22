namespace Polo3.Newsletter.Web.Services;

public enum NewsletterResultCode
{
    Subscribed,
    AlreadyActive,
    InvalidInput,
    Confirmed,
    TokenInvalid,
    Unsubscribed,
    ServiceUnavailable,
}
