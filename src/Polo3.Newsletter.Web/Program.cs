using System.Text.Encodings.Web;
using System.Text.Unicode;
using Polo3.Newsletter.Web.Repositories;
using Polo3.Newsletter.Web.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Newsletter")
    ?? throw new InvalidOperationException("Connection string 'Newsletter' is missing.");

builder.Services.AddControllersWithViews();
// Render Czech letters as-is instead of HTML entities.
builder.Services.AddWebEncoders(options => options.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));
builder.Services.Configure<NewsletterOptions>(builder.Configuration.GetSection(NewsletterOptions.SectionName));
builder.Services.AddScoped<INewsletterService, NewsletterService>();
builder.Services.AddScoped<INewsletterSubscriptionRepository>(_ => new NewsletterSubscriptionRepository(connectionString));
builder.Services.AddSingleton<INewsletterEmailSender, LogNewsletterEmailSender>();

var app = builder.Build();

// In every environment, so nobody sees the developer exception page.
app.UseExceptionHandler("/error");
app.MapControllers();
app.MapGet("/", () => Results.Redirect("/newsletter"));

await app.RunAsync();
