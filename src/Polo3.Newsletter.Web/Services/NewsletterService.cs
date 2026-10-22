using System.Buffers.Text;
using System.Data.Common;
using System.Net.Mail;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Polo3.Newsletter.Web.Models;
using Polo3.Newsletter.Web.Repositories;

namespace Polo3.Newsletter.Web.Services;

public sealed class NewsletterService(
    INewsletterSubscriptionRepository repository,
    INewsletterEmailSender emailSender,
    IOptions<NewsletterOptions> options,
    ILogger<NewsletterService> logger) : INewsletterService
{
    public const int MaxEmailLength = 254;
    public const int MaxNameLength = 100;
    public const int TokenByteLength = 32;

    /// <summary>Base64Url length of <see cref="TokenByteLength"/> bytes without padding.</summary>
    public const int TokenLength = 43;

    private readonly Uri _baseUrl = options.Value.BaseUrl;

    public Task<NewsletterResult> SubscribeAsync(NewsletterSubscribeModel model, CancellationToken cancellationToken)
    {
        var email = model.Email.Trim();
        var validationError = Validate(email, model.Name, model.IsConsent);
        if (validationError is not null)
        {
            return Task.FromResult(new NewsletterResult(NewsletterResultCode.InvalidInput, validationError));
        }

        var request = new NewsletterSyncRequest(email, model.Name, model.IsConsent, CreateToken(), CreateToken());
        return RunOnDatabaseAsync("subscribe", () => SyncAndSendAsync(request, cancellationToken));
    }

    public Task<NewsletterResult> ConfirmAsync(string token, CancellationToken cancellationToken)
    {
        if (!IsWellFormedToken(token))
        {
            return Task.FromResult(TokenInvalid());
        }

        return RunOnDatabaseAsync("confirm", async () =>
            await repository.ConfirmAsync(token, cancellationToken)
                ? new NewsletterResult(NewsletterResultCode.Confirmed, NewsletterMessages.Confirmed)
                : TokenInvalid());
    }

    public Task<NewsletterResult> UnsubscribeAsync(string token, CancellationToken cancellationToken)
    {
        if (!IsWellFormedToken(token))
        {
            return Task.FromResult(TokenInvalid());
        }

        return RunOnDatabaseAsync("unsubscribe", async () =>
            await repository.UnsubscribeAsync(token, cancellationToken) == UnsubscribeOutcome.NotFound
                ? TokenInvalid()
                : new NewsletterResult(NewsletterResultCode.Unsubscribed, NewsletterMessages.Unsubscribed));
    }

    private async Task<NewsletterResult> SyncAndSendAsync(NewsletterSyncRequest request, CancellationToken cancellationToken)
    {
        var sync = await repository.SyncAsync(request, cancellationToken);
        if (sync.ConfirmToken is null)
        {
            NewsletterLog.SubscriptionAlreadyActive(logger, request.Email);
            // Same text as for a new address, so the page does not reveal who is subscribed.
            return new NewsletterResult(NewsletterResultCode.AlreadyActive, NewsletterMessages.Subscribed);
        }

        var confirmUrl = new Uri(_baseUrl, $"newsletter/confirm/{sync.ConfirmToken}");
        var unsubscribeUrl = new Uri(_baseUrl, $"newsletter/unsubscribe/{sync.UnsubscribeToken}");
        await emailSender.SendConfirmationAsync(request.Email, confirmUrl, unsubscribeUrl, cancellationToken);
        return new NewsletterResult(NewsletterResultCode.Subscribed, NewsletterMessages.Subscribed);
    }

    /// <summary>Database failures are expected (server down, timeout): log them and give the user a message instead of a 500.</summary>
    private async Task<NewsletterResult> RunOnDatabaseAsync(string operation, Func<Task<NewsletterResult>> action)
    {
        try
        {
            return await action();
        }
        catch (DbException exception)
        {
            NewsletterLog.DatabaseFailed(logger, exception, operation);
            return new NewsletterResult(NewsletterResultCode.ServiceUnavailable, NewsletterMessages.ServiceUnavailable);
        }
    }

    private static string? Validate(string email, string? name, bool isConsent)
    {
        if (email.Length == 0)
        {
            return NewsletterMessages.EmailRequired;
        }

        if (email.Length > MaxEmailLength || !MailAddress.TryCreate(email, out var address) || address.Address != email)
        {
            return NewsletterMessages.EmailInvalid;
        }

        if (name?.Length > MaxNameLength)
        {
            return NewsletterMessages.NameTooLong;
        }

        return isConsent ? null : NewsletterMessages.ConsentRequired;
    }

    private static string CreateToken() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenByteLength));

    private static bool IsWellFormedToken(string token) =>
        token.Length == TokenLength && token.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    private static NewsletterResult TokenInvalid() => new(NewsletterResultCode.TokenInvalid, NewsletterMessages.TokenInvalid);
}
