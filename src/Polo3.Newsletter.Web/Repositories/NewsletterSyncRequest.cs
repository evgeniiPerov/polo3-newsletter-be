namespace Polo3.Newsletter.Web.Repositories;

public sealed record NewsletterSyncRequest(string Email, string? Name, bool IsConsent, string ConfirmToken, string UnsubscribeToken);
