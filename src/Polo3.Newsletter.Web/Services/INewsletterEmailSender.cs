namespace Polo3.Newsletter.Web.Services;

public interface INewsletterEmailSender
{
    Task SendConfirmationAsync(string email, Uri confirmUrl, Uri unsubscribeUrl, CancellationToken cancellationToken);
}
