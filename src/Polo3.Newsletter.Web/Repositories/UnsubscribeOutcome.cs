namespace Polo3.Newsletter.Web.Repositories;

/// <summary>Return values of dbo.UnsubscribeNewsletterSubscription.</summary>
public enum UnsubscribeOutcome
{
    NotFound = 0,
    Unsubscribed = 1,
    AlreadyUnsubscribed = 2,
}
