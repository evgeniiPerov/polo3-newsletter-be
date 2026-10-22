namespace Polo3.Newsletter.Web.Repositories;

/// <summary>Row returned by dbo.SyncNewsletterSubscriptionData; ConfirmToken is null when the subscription was already active.</summary>
public sealed record NewsletterSyncResult(NewsletterSubscriptionStatus Status, string? ConfirmToken, string UnsubscribeToken);
