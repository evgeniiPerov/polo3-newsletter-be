# Průvodní text – praktický úkol BE (registrace k odběru novinek)

## Co je hotové

- Tři akce ze zadání a formulář (`GET /newsletter`). Vrstvy: `NewsletterController` (jen validace vstupu,
  volání service, výběr view) → `NewsletterService` (logika) → `NewsletterSubscriptionRepository`
  (Dapper, výhradně uložené procedury). Závislosti přes DI v `Program.cs`.
- SQL skript: tabulka `NewsletterSubscription`, unikátní index na `Email`, unikátní indexy na tokenech
  (na `ConfirmToken` filtrovaný, protože po potvrzení je NULL), procedury `SyncNewsletterSubscriptionData`,
  `ConfirmNewsletterSubscription`, `UnsubscribeNewsletterSubscription`, 2 testovací řádky. Hlavička uvádí cílovou DB; skript lze spustit opakovaně.
- 21 jednotkových testů service vrstvy (15 metod) s falešným repository: platná a opakovaná registrace
  (i jiná velikost písmen a mezery), neplatný e-mail, chybějící souhlas, dlouhé jméno, neznámý, poškozený
  a použitý token, dvojí odhlášení, výpadek DB.
- `scripts/run-scenarios.sh`: scénáře z hodnocení plus neplatný vstup, POST bez antiforgery tokenu a 10 souběžných
  registrací stejné adresy – vše prošlo, v tabulce vždy 1 řádek na adresu, žádná odpověď 500.

## Rozhodnutí

- **Bez duplicit:** sloupec `Email` má collation `Latin1_General_100_CI_AS` (nezávisle na výchozí collation DB)
  a unikátní index. Procedura `Sync…Data` hledá záznam v transakci s `UPDLOCK, HOLDLOCK`, takže dvě souběžné
  první registrace nevloží dva řádky; index je pojistka.
- **Opakovaná registrace:** čekající záznam si ponechá svůj potvrzovací token (odkaz z prvního e-mailu dál platí
  a pošle se znovu), odhlášený dostane nový token a vrátí se do stavu „čeká na potvrzení“, aktivní se nemění. Uživatel vidí ve všech případech stejnou hlášku,
  aby stránka neprozradila, kdo novinky odebírá.
- **Token:** 32 bajtů z `RandomNumberGenerator`, Base64Url (43 znaků). `ConfirmToken` se po potvrzení nastaví
  na NULL, `UnsubscribeToken` zůstává pro odhlášení. Token s jiným tvarem se odmítne bez dotazu do DB.
  V produkci bych v DB ukládal jen hash tokenu a odkazy s tokeny nelogoval; tady log nahrazuje e-mail.
- **Chyby:** service vrací `NewsletterResult` (kód + hláška pro uživatele), obdobu `FactoryResult`. Výjimky DB
  (`DbException`) zachytí service, zaloguje přes `ILogger` a vrátí hlášku „Služba je dočasně nedostupná“.
  Všechny očekávané stavy vrací view s HTTP 200 (POST bez antiforgery tokenu odmítne framework kódem 400);
  neočekávaná chyba skončí na stránce `/error`.
- **Validace dvakrát:** atributy modelu dávají hlášky ve formuláři, service vstup ověřuje znovu, protože
  pravidla patří do business vrstvy a testují se tam. Limity délek jsou jen v jedné konstantě.
- **`data-result-code`:** atribut ve výsledkové stránce slouží skriptu scénářů, aby nezávisel na znění hlášek.
- **Název `NewsletterService`:** ponechán ze zadání; v projektu by odpovídal `…Factory`.
- **Čistota:** `TreatWarningsAsErrors`, `AnalysisLevel 10.0-recommended`, pravidla pojmenování v `.editorconfig`,
  logování přes `[LoggerMessage]`, texty hlášek na jednom místě (`NewsletterMessages`).
- **Heslo:** v repozitáři není; výchozí připojení je integrované přihlášení, jinak proměnná prostředí.

## Co jsem vynechal a proč

- Skutečné odesílání e-mailu – zadání ho nahrazuje logem (`LogNewsletterEmailSender` za rozhraním).
- Lokalizace přes resx – aplikace je jen česky; texty jsou v jedné třídě, převod na resx je přímočarý.
- Styly a klientská validace – úkol hodnotí BE; formulář má `novalidate`, aby byly vidět hlášky serveru.
- Integrační testy repository – procedury ověřuje skript scénářů proti skutečnému SQL Serveru.
- Ochrana proti zneužití (rate limiting, CAPTCHA) – mimo rozsah 2–4 h.

## Položky kontrolního seznamu

Mimo rozsah BE: A, C, D, E, I. J1 (vývojové prostředí, tři šířky, tmavý režim) nahrazuje ověření
proti SQL Serveru 2022 v Dockeru skriptem scénářů. F2, G1 a G7 uvádí hodnoticí list, v seznamu v1.0 nejsou;
chápu je jako DI mezi vrstvami, tabulku s indexem a procedurami a skript s hlavičkou.
