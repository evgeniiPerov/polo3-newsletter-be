namespace Polo3.Newsletter.Web.Services;

/// <summary>Stands in for real email delivery, as the assignment asks: writes the address and links to the log.</summary>
public sealed class LogNewsletterEmailSender(ILogger<LogNewsletterEmailSender> logger) : INewsletterEmailSender
{
    public Task SendConfirmationAsync(string email, Uri confirmUrl, Uri unsubscribeUrl, CancellationToken cancellationToken)
    {
        NewsletterLog.ConfirmationLinkCreated(logger, email, confirmUrl, unsubscribeUrl);
        return Task.CompletedTask;
    }
}
