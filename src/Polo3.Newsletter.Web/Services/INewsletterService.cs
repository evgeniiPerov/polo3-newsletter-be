using Polo3.Newsletter.Web.Models;

namespace Polo3.Newsletter.Web.Services;

public interface INewsletterService
{
    Task<NewsletterResult> SubscribeAsync(NewsletterSubscribeModel model, CancellationToken cancellationToken);

    Task<NewsletterResult> ConfirmAsync(string token, CancellationToken cancellationToken);

    Task<NewsletterResult> UnsubscribeAsync(string token, CancellationToken cancellationToken);
}
