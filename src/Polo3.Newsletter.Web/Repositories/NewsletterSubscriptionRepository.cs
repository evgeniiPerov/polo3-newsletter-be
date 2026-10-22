using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

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
            email = new DbString { Value = request.Email, Length = NewsletterLimits.MaxEmailLength },
            name = new DbString { Value = request.Name, Length = NewsletterLimits.MaxNameLength },
            isConsent = request.IsConsent,
            confirmToken = CreateTokenParameter(request.ConfirmToken),
            unsubscribeToken = CreateTokenParameter(request.UnsubscribeToken),
        };

        await using var connection = new SqlConnection(connectionString);
        var row = await connection.QuerySingleAsync<SyncRow>(CreateProcedureCommand(SyncProcedure, parameters, cancellationToken));
        if (!Enum.IsDefined((NewsletterSubscriptionStatus)row.Status))
        {
            throw new InvalidOperationException($"{SyncProcedure} returned unknown status {row.Status}.");
        }

        return new NewsletterSyncResult((NewsletterSubscriptionStatus)row.Status, row.ConfirmToken, row.UnsubscribeToken);
    }

    public async Task<bool> ConfirmAsync(string confirmToken, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        var confirmedCount = await connection.QuerySingleAsync<int>(
            CreateProcedureCommand(ConfirmProcedure, new { confirmToken = CreateTokenParameter(confirmToken) }, cancellationToken));
        return confirmedCount == 1;
    }

    public async Task<UnsubscribeOutcome> UnsubscribeAsync(string unsubscribeToken, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        var outcome = await connection.QuerySingleAsync<int>(
            CreateProcedureCommand(UnsubscribeProcedure, new { unsubscribeToken = CreateTokenParameter(unsubscribeToken) }, cancellationToken));
        if (!Enum.IsDefined((UnsubscribeOutcome)outcome))
        {
            throw new InvalidOperationException($"{UnsubscribeProcedure} returned unknown outcome {outcome}.");
        }

        return (UnsubscribeOutcome)outcome;
    }

    private static CommandDefinition CreateProcedureCommand(string name, object parameters, CancellationToken cancellationToken) =>
        new(name, parameters, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken);

    // Tokens are varchar columns; an ANSI (varchar) parameter keeps the comparison on the column type so the index can be used.
    private static DbString CreateTokenParameter(string value) => new() { Value = value, IsAnsi = true, Length = TokenColumnLength };

    // Dapper maps by property name; a plain row class avoids its strict constructor matching for tinyint → enum.
    private sealed class SyncRow
    {
        public byte Status { get; set; }

        public string? ConfirmToken { get; set; }

        public string UnsubscribeToken { get; set; } = string.Empty;
    }
}
