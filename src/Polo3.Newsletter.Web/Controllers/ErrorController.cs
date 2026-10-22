using Microsoft.AspNetCore.Mvc;

namespace Polo3.Newsletter.Web.Controllers;

/// <summary>Target of UseExceptionHandler: unexpected errors get a friendly page, details stay in the log.</summary>
[Route("error")]
public sealed class ErrorController : Controller
{
    public IActionResult Index() => View("Error");
}
