using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Polo3.Newsletter.Web.Services;

namespace Polo3.Newsletter.Web.Repositories;

/// <summary>Database access through the stored procedures in db/NewsletterSubscription.sql only.</summary>
public sealed class NewsletterSubscriptionRepository(string connectionString) : INewsletterSubscriptionRepository
{
    private const string SyncProcedure = "dbo.SyncNewsletterSubscriptionData";
    private const string ConfirmProcedure = "dbo.ConfirmNewsletterSubscription";
    private const string UnsubscribeProcedure = "dbo.UnsubscribeNewsletterSubscription";
    private const int TokenColumnLength = 64;

    public async Task<NewsletterSyncResult> SyncAsync(NewsletterSyncRequest request, CancellationToken cancellationToken)
    {
        var parameters = new
        {
            email = new DbString { Value = request.Email, Length = NewsletterService.MaxEmailLength },
            name = new DbString { Value = request.Name, Length = NewsletterService.MaxNameLength },
            isConsent = request.IsConsent,
            confirmToken = Token(request.ConfirmToken),
            unsubscribeToken = Token(request.UnsubscribeToken),
        };

        await using var connection = new SqlConnection(connectionString);
        var row = await connection.QuerySingleAsync<SyncRow>(Procedure(SyncProcedure, parameters, cancellationToken));
        return new NewsletterSyncResult((NewsletterSubscriptionStatus)row.Status, row.ConfirmToken, row.UnsubscribeToken, row.IsNew);
    }

    public async Task<bool> ConfirmAsync(string confirmToken, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        var confirmedCount = await connection.ExecuteScalarAsync<int>(
            Procedure(ConfirmProcedure, new { confirmToken = Token(confirmToken) }, cancellationToken));
        return confirmedCount == 1;
    }

    public async Task<UnsubscribeOutcome> UnsubscribeAsync(string unsubscribeToken, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        var outcome = await connection.ExecuteScalarAsync<int>(
            Procedure(UnsubscribeProcedure, new { unsubscribeToken = Token(unsubscribeToken) }, cancellationToken));
        return (UnsubscribeOutcome)outcome;
    }

    private static CommandDefinition Procedure(string name, object parameters, CancellationToken cancellationToken) =>
        new(name, parameters, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken);

    // Tokens are varchar columns; an ANSI parameter avoids an nvarchar-to-varchar conversion that would skip the index.
    private static DbString Token(string value) => new() { Value = value, IsAnsi = true, Length = TokenColumnLength };

    // Dapper maps by property name; a plain row class avoids its strict constructor matching for tinyint → enum.
    private sealed class SyncRow
    {
        public byte Status { get; set; }

        public string? ConfirmToken { get; set; }

        public string UnsubscribeToken { get; set; } = string.Empty;

        public bool IsNew { get; set; }
    }
}
