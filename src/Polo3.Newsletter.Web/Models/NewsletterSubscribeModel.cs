using System.ComponentModel.DataAnnotations;
using Polo3.Newsletter.Web.Services;

namespace Polo3.Newsletter.Web.Models;

public sealed class NewsletterSubscribeModel
{
    [Display(Name = "E-mail")]
    [Required(ErrorMessage = NewsletterMessages.EmailRequired)]
    [EmailAddress(ErrorMessage = NewsletterMessages.EmailInvalid)]
    [StringLength(NewsletterService.MaxEmailLength, ErrorMessage = NewsletterMessages.EmailInvalid)]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Jméno")]
    [StringLength(NewsletterService.MaxNameLength, ErrorMessage = NewsletterMessages.NameTooLong)]
    public string? Name { get; set; }

    [Display(Name = "Souhlasím se zpracováním e-mailu pro zasílání novinek")]
    // MVC adds an implicit [Required] with an English text to non-nullable value types; this one sets the Czech text.
    [Required(ErrorMessage = NewsletterMessages.ConsentRequired)]
    [Range(typeof(bool), "true", "true", ErrorMessage = NewsletterMessages.ConsentRequired)]
    public bool IsConsent { get; set; }
}
