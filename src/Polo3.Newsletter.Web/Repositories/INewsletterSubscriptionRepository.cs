namespace Polo3.Newsletter.Web.Repositories;

public interface INewsletterSubscriptionRepository
{
    Task<NewsletterSyncResult> SyncAsync(NewsletterSyncRequest request, CancellationToken cancellationToken);

    Task<bool> ConfirmAsync(string confirmToken, CancellationToken cancellationToken);

    Task<UnsubscribeOutcome> UnsubscribeAsync(string unsubscribeToken, CancellationToken cancellationToken);
}
