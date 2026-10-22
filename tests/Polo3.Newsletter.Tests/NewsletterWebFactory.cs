using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Polo3.Newsletter.Web.Repositories;
using Polo3.Newsletter.Web.Services;

namespace Polo3.Newsletter.Tests;

/// <summary>Starts the real app with the in-memory repository and a recording email sender, so no SQL Server is needed.</summary>
public sealed class NewsletterWebFactory : WebApplicationFactory<Program>
{
    public FakeNewsletterSubscriptionRepository Repository { get; } = new();

    public FakeNewsletterEmailSender EmailSender { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Program.cs throws when the connection string is missing; the fake repository never uses it.
        builder.UseSetting("ConnectionStrings:Newsletter", "Server=unused;Database=unused");
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<INewsletterSubscriptionRepository>(Repository);
            services.AddSingleton<INewsletterEmailSender>(EmailSender);
        });
    }
}
