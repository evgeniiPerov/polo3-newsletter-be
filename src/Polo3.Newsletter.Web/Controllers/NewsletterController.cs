using Microsoft.AspNetCore.Mvc;
using Polo3.Newsletter.Web.Models;
using Polo3.Newsletter.Web.Services;

namespace Polo3.Newsletter.Web.Controllers;

[Route("newsletter")]
public sealed class NewsletterController(INewsletterService newsletterService) : Controller
{
    private const string ResultView = "Result";

    [HttpGet("")]
    public IActionResult Index() => View(new NewsletterSubscribeModel());

    [HttpPost("subscribe")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Subscribe(NewsletterSubscribeModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(nameof(Index), model);
        }

        var outcome = await newsletterService.SubscribeAsync(model, cancellationToken);
        if (!outcome.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, outcome.Message);
            ViewData[nameof(NewsletterResultCode)] = outcome.Code;
            return View(nameof(Index), model);
        }

        return View(ResultView, outcome);
    }

    [HttpGet("confirm/{token}")]
    public async Task<IActionResult> Confirm(string token, CancellationToken cancellationToken) =>
        View(ResultView, await newsletterService.ConfirmAsync(token, cancellationToken));

    [HttpGet("unsubscribe/{token}")]
    public async Task<IActionResult> Unsubscribe(string token, CancellationToken cancellationToken) =>
        View(ResultView, await newsletterService.UnsubscribeAsync(token, cancellationToken));
}
