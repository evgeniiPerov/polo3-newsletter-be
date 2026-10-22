using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Polo3.Newsletter.Web.Models;
using Polo3.Newsletter.Web.Repositories;
using Polo3.Newsletter.Web.Services;

namespace Polo3.Newsletter.Tests;

public sealed class NewsletterServiceTests
{
    private const string Email = "jan@example.com";

    private readonly FakeNewsletterSubscriptionRepository _repository = new();
    private readonly FakeNewsletterEmailSender _emailSender = new();
    private readonly NewsletterService _service;

    public static TheoryData<string> InvalidEmails =>
    [
        "",
        "not-an-email",
        "a@b@c",
        "Jan Novak <jan@example.com>",
        new string('a', 245) + "@example.com",
    ];

    public static TheoryData<string> MalformedTokens =>
    [
        "abc",
        "has space" + new string('a', 34),
        new string('a', 500),
    ];

    public NewsletterServiceTests()
    {
        var options = Options.Create(new NewsletterOptions { BaseUrl = new Uri("https://shop.test") });
        _service = new NewsletterService(_repository, _emailSender, options, NullLogger<NewsletterService>.Instance);
    }

    [Fact]
    public async Task SubscribeWithValidInputCreatesPendingSubscriptionAndSendsLinks()
    {
        var result = await SubscribeAsync(Email);

        Assert.Equal(NewsletterResultCode.Subscribed, result.Code);
        Assert.Equal(NewsletterMessages.Subscribed, result.Message);
        Assert.Equal(1, _repository.Count);
        var row = _repository.Get(Email);
        Assert.Equal(NewsletterSubscriptionStatus.Pending, row.Status);
        Assert.NotNull(row.ConfirmToken);
        Assert.Matches("^[A-Za-z0-9_-]{43}$", row.ConfirmToken);
        Assert.Matches("^[A-Za-z0-9_-]{43}$", row.UnsubscribeToken);
        Assert.NotEqual(row.ConfirmToken, row.UnsubscribeToken);
        var sent = Assert.Single(_emailSender.Sent);
        Assert.Equal(Email, sent.Email);
        Assert.Equal(new Uri($"https://shop.test/newsletter/confirm/{row.ConfirmToken}"), sent.ConfirmUrl);
        Assert.Equal(new Uri($"https://shop.test/newsletter/unsubscribe/{row.UnsubscribeToken}"), sent.UnsubscribeUrl);
    }

    [Fact]
    public async Task SubscribeWithSameEmailDifferentCaseKeepsOneRecord()
    {
        var first = await SubscribeAsync("Jan@Example.com");
        var firstToken = _repository.Get(Email).ConfirmToken;
        var second = await SubscribeAsync("jan@example.com");

        Assert.Equal(NewsletterResultCode.Subscribed, first.Code);
        Assert.Equal(NewsletterResultCode.Subscribed, second.Code);
        Assert.Equal(1, _repository.Count);
        Assert.Equal(2, _emailSender.Sent.Count);
        Assert.NotEqual(firstToken, _repository.Get(Email).ConfirmToken);
    }

    [Fact]
    public async Task SubscribeWithSurroundingSpacesUsesTrimmedEmail()
    {
        await SubscribeAsync(" jan@example.com ");
        await SubscribeAsync(Email);

        Assert.Equal(1, _repository.Count);
        Assert.Equal(Email, _emailSender.Sent[0].Email);
    }

    [Fact]
    public async Task SubscribeWhenAlreadyActiveReturnsNeutralMessageWithoutLink()
    {
        await SubscribeAsync(Email);
        await _service.ConfirmAsync(_repository.Get(Email).ConfirmToken!, CancellationToken.None);

        var result = await SubscribeAsync(Email);

        Assert.Equal(NewsletterResultCode.AlreadyActive, result.Code);
        Assert.Equal(NewsletterMessages.Subscribed, result.Message);
        Assert.Single(_emailSender.Sent);
    }

    [Theory]
    [MemberData(nameof(InvalidEmails))]
    public async Task SubscribeWithInvalidEmailReturnsInvalidInput(string email)
    {
        var result = await SubscribeAsync(email);

        Assert.Equal(NewsletterResultCode.InvalidInput, result.Code);
        Assert.False(result.IsSuccess);
        Assert.Equal(0, _repository.CallCount);
    }

    [Fact]
    public async Task SubscribeWithTooLongNameReturnsInvalidInput()
    {
        var result = await SubscribeAsync(Email, name: new string('a', 101));

        Assert.Equal(NewsletterResultCode.InvalidInput, result.Code);
        Assert.Equal(NewsletterMessages.NameTooLong, result.Message);
        Assert.Equal(0, _repository.CallCount);
    }

    [Fact]
    public async Task SubscribeWithoutConsentReturnsInvalidInput()
    {
        var result = await SubscribeAsync(Email, isConsent: false);

        Assert.Equal(NewsletterResultCode.InvalidInput, result.Code);
        Assert.Equal(NewsletterMessages.ConsentRequired, result.Message);
        Assert.Equal(0, _repository.CallCount);
    }

    [Fact]
    public async Task ConfirmWithUnknownTokenReturnsTokenInvalid()
    {
        var result = await _service.ConfirmAsync(new string('A', 43), CancellationToken.None);

        Assert.Equal(NewsletterResultCode.TokenInvalid, result.Code);
        Assert.Equal(NewsletterMessages.TokenInvalid, result.Message);
        Assert.False(result.IsSuccess);
    }

    [Theory]
    [MemberData(nameof(MalformedTokens))]
    public async Task ConfirmWithMalformedTokenSkipsRepository(string token)
    {
        var result = await _service.ConfirmAsync(token, CancellationToken.None);

        Assert.Equal(NewsletterResultCode.TokenInvalid, result.Code);
        Assert.Equal(0, _repository.CallCount);
    }

    [Fact]
    public async Task ConfirmWithUsedTokenReturnsTokenInvalid()
    {
        await SubscribeAsync(Email);
        var token = _repository.Get(Email).ConfirmToken!;

        var first = await _service.ConfirmAsync(token, CancellationToken.None);
        var second = await _service.ConfirmAsync(token, CancellationToken.None);

        Assert.Equal(NewsletterResultCode.Confirmed, first.Code);
        Assert.Equal(NewsletterMessages.Confirmed, first.Message);
        Assert.Equal(NewsletterSubscriptionStatus.Active, _repository.Get(Email).Status);
        Assert.Equal(NewsletterResultCode.TokenInvalid, second.Code);
    }

    [Fact]
    public async Task UnsubscribeTwiceReturnsUnsubscribedBothTimes()
    {
        await SubscribeAsync(Email);
        var token = _repository.Get(Email).UnsubscribeToken;

        var first = await _service.UnsubscribeAsync(token, CancellationToken.None);
        var second = await _service.UnsubscribeAsync(token, CancellationToken.None);

        Assert.Equal(NewsletterResultCode.Unsubscribed, first.Code);
        Assert.Equal(NewsletterResultCode.Unsubscribed, second.Code);
        Assert.Equal(NewsletterMessages.Unsubscribed, second.Message);
        Assert.Equal(NewsletterSubscriptionStatus.Unsubscribed, _repository.Get(Email).Status);
    }

    [Fact]
    public async Task UnsubscribeWithUnknownTokenReturnsTokenInvalid()
    {
        var result = await _service.UnsubscribeAsync(new string('B', 43), CancellationToken.None);

        Assert.Equal(NewsletterResultCode.TokenInvalid, result.Code);
    }

    [Fact]
    public async Task SubscribeWhenDatabaseFailsReturnsServiceUnavailable()
    {
        _repository.FailWith = new FakeDbException();

        var result = await SubscribeAsync(Email);

        Assert.Equal(NewsletterResultCode.ServiceUnavailable, result.Code);
        Assert.Equal(NewsletterMessages.ServiceUnavailable, result.Message);
        Assert.Empty(_emailSender.Sent);
    }

    [Fact]
    public async Task ConfirmWhenDatabaseFailsReturnsServiceUnavailable()
    {
        _repository.FailWith = new FakeDbException();

        var result = await _service.ConfirmAsync(new string('A', 43), CancellationToken.None);

        Assert.Equal(NewsletterResultCode.ServiceUnavailable, result.Code);
        Assert.Equal(NewsletterMessages.ServiceUnavailable, result.Message);
    }

    private Task<NewsletterResult> SubscribeAsync(string email, string? name = null, bool isConsent = true)
    {
        var model = new NewsletterSubscribeModel { Email = email, Name = name, IsConsent = isConsent };
        return _service.SubscribeAsync(model, CancellationToken.None);
    }
}
