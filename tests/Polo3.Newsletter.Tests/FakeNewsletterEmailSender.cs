using Polo3.Newsletter.Web.Services;

namespace Polo3.Newsletter.Tests;

public sealed class FakeNewsletterEmailSender : INewsletterEmailSender
{
    public List<SentConfirmation> Sent { get; } = [];

    public Task SendConfirmationAsync(string email, Uri confirmUrl, Uri unsubscribeUrl, CancellationToken cancellationToken)
    {
        Sent.Add(new SentConfirmation(email, confirmUrl, unsubscribeUrl));
        return Task.CompletedTask;
    }

    public sealed record SentConfirmation(string Email, Uri ConfirmUrl, Uri UnsubscribeUrl);
}
