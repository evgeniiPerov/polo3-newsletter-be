using System.Net;
using System.Text.RegularExpressions;
using Polo3.Newsletter.Web;
using Polo3.Newsletter.Web.Services;

namespace Polo3.Newsletter.Tests;

/// <summary>
/// Unit and HTTP tests prove the service's upsert rules with the in-memory fake. The database-level duplicate
/// guarantee (case-insensitive collation, unique index, lock hints, 10 parallel requests) is proven by
/// scripts/run-scenarios.sh against a real SQL Server.
/// </summary>
public sealed partial class NewsletterEndpointTests : IDisposable
{
    private const string Email = "jan@example.com";

    private readonly NewsletterWebFactory _factory = new();
    private readonly HttpClient _client;

    public NewsletterEndpointTests()
    {
        _client = _factory.CreateClient();
    }

    public static TheoryData<string> LinkRoutes => ["confirm", "unsubscribe"];

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Theory]
    [MemberData(nameof(LinkRoutes))]
    public async Task LinkWithUnknownTokenReturnsTokenInvalidPage(string route)
    {
        var page = await GetPageAsync($"/newsletter/{route}/{new string('a', NewsletterLimits.TokenLength)}");

        AssertPage(page, NewsletterResultCode.TokenInvalid, NewsletterMessages.TokenInvalid);
    }

    [Theory]
    [MemberData(nameof(LinkRoutes))]
    public async Task LinkWithMalformedTokenReturnsTokenInvalidPage(string route)
    {
        var page = await GetPageAsync($"/newsletter/{route}/abc");

        AssertPage(page, NewsletterResultCode.TokenInvalid, NewsletterMessages.TokenInvalid);
    }

    [Theory]
    [MemberData(nameof(LinkRoutes))]
    public async Task LinkWithOverlongTokenReturnsTokenInvalidPage(string route)
    {
        var page = await GetPageAsync($"/newsletter/{route}/{new string('a', 500)}");

        AssertPage(page, NewsletterResultCode.TokenInvalid, NewsletterMessages.TokenInvalid);
    }

    [Fact]
    public async Task SubscribeWhenDatabaseFailsShowsServiceUnavailable()
    {
        _factory.Repository.FailWith = new FakeDbException();

        var page = await PostSubscribeAsync(Email);

        AssertPage(page, NewsletterResultCode.ServiceUnavailable, NewsletterMessages.ServiceUnavailable);
    }

    [Fact]
    public async Task UnsubscribeWhenDatabaseFailsShowsServiceUnavailable()
    {
        _factory.Repository.FailWith = new FakeDbException();

        var page = await GetPageAsync($"/newsletter/unsubscribe/{new string('a', NewsletterLimits.TokenLength)}");

        AssertPage(page, NewsletterResultCode.ServiceUnavailable, NewsletterMessages.ServiceUnavailable);
    }

    [Fact]
    public async Task ResubscribeAfterUnsubscribeSendsNewWorkingConfirmLink()
    {
        await PostSubscribeAsync(Email);
        var first = _factory.EmailSender.Sent[^1];
        AssertPage(await GetPageAsync(first.ConfirmUrl.AbsolutePath), NewsletterResultCode.Confirmed, NewsletterMessages.Confirmed);
        AssertPage(await GetPageAsync(first.UnsubscribeUrl.AbsolutePath), NewsletterResultCode.Unsubscribed, NewsletterMessages.Unsubscribed);

        AssertPage(await PostSubscribeAsync(Email), NewsletterResultCode.Subscribed, NewsletterMessages.Subscribed);

        var second = _factory.EmailSender.Sent[^1];
        Assert.Equal(2, _factory.EmailSender.Sent.Count);
        Assert.NotEqual(first.ConfirmUrl, second.ConfirmUrl);
        AssertPage(await GetPageAsync(second.ConfirmUrl.AbsolutePath), NewsletterResultCode.Confirmed, NewsletterMessages.Confirmed);
    }

    [Fact]
    public async Task ConfirmAfterUnsubscribeReturnsTokenInvalid()
    {
        await PostSubscribeAsync(Email);
        var sent = _factory.EmailSender.Sent[^1];
        AssertPage(await GetPageAsync(sent.UnsubscribeUrl.AbsolutePath), NewsletterResultCode.Unsubscribed, NewsletterMessages.Unsubscribed);

        var page = await GetPageAsync(sent.ConfirmUrl.AbsolutePath);

        AssertPage(page, NewsletterResultCode.TokenInvalid, NewsletterMessages.TokenInvalid);
    }

    [Fact]
    public async Task PostWithoutAntiforgeryTokenReturnsBadRequest()
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = Email,
            ["IsConsent"] = "true",
        });

        using var response = await _client.PostAsync("/newsletter/subscribe", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, _factory.Repository.CallCount);
    }

    private static void AssertPage(Page page, NewsletterResultCode expectedCode, string expectedText)
    {
        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(expectedCode.ToString(), page.Code);
        Assert.Contains(expectedText, page.Body, StringComparison.Ordinal);
    }

    private async Task<Page> GetPageAsync(string path)
    {
        using var response = await _client.GetAsync(path);
        return await ReadPageAsync(response);
    }

    /// <summary>GETs the form for the antiforgery token (the client keeps the cookie), then posts it with the form fields.</summary>
    private async Task<Page> PostSubscribeAsync(string email)
    {
        var form = await _client.GetStringAsync("/newsletter");
        var token = AntiforgeryTokenPattern().Match(form).Groups[1].Value;
        Assert.NotEmpty(token);

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["IsConsent"] = "true",
            ["__RequestVerificationToken"] = token,
        });
        using var response = await _client.PostAsync("/newsletter/subscribe", content);
        return await ReadPageAsync(response);
    }

    private static async Task<Page> ReadPageAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        var match = ResultCodePattern().Match(body);
        return new Page(response.StatusCode, match.Success ? match.Groups[1].Value : null, body);
    }

    [GeneratedRegex(@"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""")]
    private static partial Regex AntiforgeryTokenPattern();

    [GeneratedRegex(@"data-result-code=""([^""]*)""")]
    private static partial Regex ResultCodePattern();

    private sealed record Page(HttpStatusCode Status, string? Code, string Body);
}
