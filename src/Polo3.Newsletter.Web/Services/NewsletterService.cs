using System.Buffers.Text;
using System.Data.Common;
using System.Globalization;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
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
    private static readonly CompositeFormat _nameTooLongFormat = CompositeFormat.Parse(NewsletterMessages.NameTooLong);

    private readonly Uri _baseUrl = options.Value.BaseUrl;

    public Task<NewsletterResult> SubscribeAsync(NewsletterSubscribeModel model, CancellationToken cancellationToken)
    {
        var email = model.Email.Trim();
        var name = string.IsNullOrWhiteSpace(model.Name) ? null : model.Name.Trim();
        var validationError = Validate(email, name, model.IsConsent);
        if (validationError is not null)
        {
            return Task.FromResult(new NewsletterResult(NewsletterResultCode.InvalidInput, validationError));
        }

        var request = new NewsletterSyncRequest(email, name, model.IsConsent, CreateToken(), CreateToken());
        return RunOnDatabaseAsync("subscribe", () => SyncAndSendAsync(request, cancellationToken), cancellationToken);
    }

    public Task<NewsletterResult> ConfirmAsync(string token, CancellationToken cancellationToken)
    {
        if (!IsWellFormedToken(token))
        {
            return Task.FromResult(CreateTokenInvalidResult());
        }

        return RunOnDatabaseAsync("confirm", async () =>
            await repository.ConfirmAsync(token, cancellationToken)
                ? new NewsletterResult(NewsletterResultCode.Confirmed, NewsletterMessages.Confirmed)
                : CreateTokenInvalidResult(), cancellationToken);
    }

    public Task<NewsletterResult> UnsubscribeAsync(string token, CancellationToken cancellationToken)
    {
        if (!IsWellFormedToken(token))
        {
            return Task.FromResult(CreateTokenInvalidResult());
        }

        return RunOnDatabaseAsync("unsubscribe", async () =>
            await repository.UnsubscribeAsync(token, cancellationToken) == UnsubscribeOutcome.NotFound
                ? CreateTokenInvalidResult()
                : new NewsletterResult(NewsletterResultCode.Unsubscribed, NewsletterMessages.Unsubscribed), cancellationToken);
    }

    private async Task<NewsletterResult> SyncAndSendAsync(NewsletterSyncRequest request, CancellationToken cancellationToken)
    {
        var sync = await repository.SyncAsync(request, cancellationToken);
        if (sync.Status == NewsletterSubscriptionStatus.Active)
        {
            NewsletterLog.SubscriptionAlreadyActive(logger, request.Email);
            // Same text as for a new address, so the page does not reveal who is subscribed.
            return new NewsletterResult(NewsletterResultCode.Subscribed, NewsletterMessages.Subscribed);
        }

        var confirmUrl = new Uri(_baseUrl, $"newsletter/confirm/{sync.ConfirmToken}");
        var unsubscribeUrl = new Uri(_baseUrl, $"newsletter/unsubscribe/{sync.UnsubscribeToken}");
        await emailSender.SendConfirmationAsync(request.Email, confirmUrl, unsubscribeUrl, cancellationToken);
        return new NewsletterResult(NewsletterResultCode.Subscribed, NewsletterMessages.Subscribed);
    }

    /// <summary>Database failures are expected (server down, timeout): log them and give the user a message instead of a 500.</summary>
    private async Task<NewsletterResult> RunOnDatabaseAsync(string operation, Func<Task<NewsletterResult>> action, CancellationToken cancellationToken)
    {
        try
        {
            return await action();
        }
        catch (DbException exception)
        {
            // Server down, login or query failure reported by the provider.
            NewsletterLog.DatabaseFailed(logger, exception, operation);
            return CreateServiceUnavailableResult();
        }
        catch (InvalidOperationException exception)
        {
            // SqlClient reports an exhausted connection pool (timeout waiting for a free connection) this way, not as a DbException.
            NewsletterLog.DatabaseFailed(logger, exception, operation);
            return CreateServiceUnavailableResult();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The client disconnected or aborted the request; nobody reads the answer, so this is not an error.
            NewsletterLog.RequestCancelled(logger, operation);
            return CreateServiceUnavailableResult();
        }
    }

    private static string? Validate(string email, string? name, bool isConsent)
    {
        if (email.Length == 0)
        {
            return NewsletterMessages.EmailRequired;
        }

        if (email.Length > NewsletterLimits.MaxEmailLength || !MailAddress.TryCreate(email, out var address) || address.Address != email)
        {
            return NewsletterMessages.EmailInvalid;
        }

        if (name?.Length > NewsletterLimits.MaxNameLength)
        {
            return string.Format(CultureInfo.InvariantCulture, _nameTooLongFormat, nameof(NewsletterSubscribeModel.Name), NewsletterLimits.MaxNameLength);
        }

        return isConsent ? null : NewsletterMessages.ConsentRequired;
    }

    private static string CreateToken() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(NewsletterLimits.TokenByteLength));

    private static bool IsWellFormedToken(string token) =>
        token.Length == NewsletterLimits.TokenLength && token.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    private static NewsletterResult CreateTokenInvalidResult() => new(NewsletterResultCode.TokenInvalid, NewsletterMessages.TokenInvalid);

    private static NewsletterResult CreateServiceUnavailableResult() => new(NewsletterResultCode.ServiceUnavailable, NewsletterMessages.ServiceUnavailable);
}
