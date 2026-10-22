namespace Polo3.Newsletter.Web.Repositories;

/// <summary>Values of NewsletterSubscription.Status (see CK_NewsletterSubscription_Status).</summary>
public enum NewsletterSubscriptionStatus : byte
{
    Pending = 1,
    Active = 2,
    Unsubscribed = 3,
}
