using System.Data.Common;
using Polo3.Newsletter.Web.Repositories;

namespace Polo3.Newsletter.Tests;

/// <summary>In-memory stand-in for the stored procedures, with the same rules as db/NewsletterSubscription.sql.</summary>
public sealed class FakeNewsletterSubscriptionRepository : INewsletterSubscriptionRepository
{
    private readonly Dictionary<string, SubscriptionRow> _rows = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _rows.Count;

    public int CallCount { get; private set; }

    public Exception? FailWith { get; set; }

    public SubscriptionRow Get(string email) => _rows[email];

    public Task<NewsletterSyncResult> SyncAsync(NewsletterSyncRequest request, CancellationToken cancellationToken)
    {
        RegisterCall();
        var isNew = !_rows.TryGetValue(request.Email, out var row);
        if (row is null)
        {
            row = new SubscriptionRow(request.UnsubscribeToken);
            _rows[request.Email] = row;
        }

        if (row.Status != NewsletterSubscriptionStatus.Active)
        {
            row.Status = NewsletterSubscriptionStatus.Pending;
            row.ConfirmToken = request.ConfirmToken;
        }

        return Task.FromResult(new NewsletterSyncResult(row.Status, row.ConfirmToken, row.UnsubscribeToken, isNew));
    }

    public Task<bool> ConfirmAsync(string confirmToken, CancellationToken cancellationToken)
    {
        RegisterCall();
        var row = _rows.Values.SingleOrDefault(r => r.ConfirmToken == confirmToken && r.Status == NewsletterSubscriptionStatus.Pending);
        if (row is null)
        {
            return Task.FromResult(false);
        }

        row.Status = NewsletterSubscriptionStatus.Active;
        row.ConfirmToken = null;
        return Task.FromResult(true);
    }

    public Task<UnsubscribeOutcome> UnsubscribeAsync(string unsubscribeToken, CancellationToken cancellationToken)
    {
        RegisterCall();
        var row = _rows.Values.SingleOrDefault(r => r.UnsubscribeToken == unsubscribeToken);
        if (row is null)
        {
            return Task.FromResult(UnsubscribeOutcome.NotFound);
        }

        if (row.Status == NewsletterSubscriptionStatus.Unsubscribed)
        {
            return Task.FromResult(UnsubscribeOutcome.AlreadyUnsubscribed);
        }

        row.Status = NewsletterSubscriptionStatus.Unsubscribed;
        row.ConfirmToken = null;
        return Task.FromResult(UnsubscribeOutcome.Unsubscribed);
    }

    private void RegisterCall()
    {
        CallCount++;
        if (FailWith is not null)
        {
            throw FailWith;
        }
    }

    public sealed class SubscriptionRow(string unsubscribeToken)
    {
        public NewsletterSubscriptionStatus Status { get; set; } = NewsletterSubscriptionStatus.Pending;

        public string? ConfirmToken { get; set; }

        public string UnsubscribeToken { get; } = unsubscribeToken;
    }
}

public sealed class FakeDbException : DbException
{
    public FakeDbException()
        : base("Simulated database failure.")
    {
    }
}
