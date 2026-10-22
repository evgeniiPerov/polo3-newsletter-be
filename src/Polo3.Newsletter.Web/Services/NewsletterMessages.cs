namespace Polo3.Newsletter.Web.Services;

/// <summary>All user-facing texts in one place; const so validation attributes can use them too.</summary>
public static class NewsletterMessages
{
    public const string EmailRequired = "Vyplňte e-mail.";
    public const string EmailInvalid = "Zadejte platnou e-mailovou adresu.";
    public const string NameTooLong = "Jméno může mít nejvýše 100 znaků.";
    public const string ConsentRequired = "Pro odběr novinek je nutný souhlas se zpracováním e-mailu.";
    public const string Subscribed = "Děkujeme. Pokud adresa ještě není přihlášená, poslali jsme na ni odkaz k potvrzení odběru.";
    public const string Confirmed = "Odběr novinek je potvrzený.";
    public const string TokenInvalid = "Odkaz je neplatný nebo už byl použit.";
    public const string Unsubscribed = "Odběr novinek je zrušený.";
    public const string ServiceUnavailable = "Služba je dočasně nedostupná. Zkuste to prosím později.";
}
