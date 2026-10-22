# Průvodní text – praktický úkol BE (registrace k odběru novinek)

## Co je hotové

- Tři akce ze zadání a formulář (`GET /newsletter`). Vrstvy: `NewsletterController` (validace vstupu, výběr view)
  → `NewsletterService` (logika) → `NewsletterSubscriptionRepository` (Dapper, jen uložené procedury); DI v `Program.cs`.
- SQL skript: tabulka `NewsletterSubscription`, unikátní indexy na `Email` a tokenech (na `ConfirmToken`
  filtrovaný, po potvrzení je NULL), procedury `SyncNewsletterSubscriptionData`, `ConfirmNewsletterSubscription`,
  `UnsubscribeNewsletterSubscription`, 2 testovací řádky; lze spustit opakovaně.
- 39 testů: 28 jednotkových (service s falešným repository) a 11 HTTP testů přes `WebApplicationFactory`.
- `scripts/run-scenarios.sh`: scénáře z hodnocení plus neplatný vstup, POST bez antiforgery tokenu, opakovaná
  registrace aktivního e-mailu a 10 souběžných registrací – 1 řádek na adresu, žádná odpověď 500.

## Rozhodnutí

- **Bez duplicit:** sloupec `Email` má collation `Latin1_General_100_CI_AS` a unikátní index. Procedura
  `Sync…Data` hledá záznam s `UPDLOCK, HOLDLOCK`, takže souběžné první registrace nevloží dva řádky.
- **Opakovaná registrace:** čekající záznam si ponechá potvrzovací token (starší odkaz dál platí), jméno
  a souhlas se přepíšou; odhlášený dostane nový potvrzovací token a vrátí se do stavu „čeká na potvrzení“, aktivní
  se nemění a e-mail se mu neposílá. Hláška je vždy stejná, aby stránka neprozradila, kdo novinky odebírá.
- **Token:** 32 bajtů z `RandomNumberGenerator`, Base64Url (43 znaků). `ConfirmToken` je po potvrzení NULL,
  `UnsubscribeToken` zůstává. V produkci bych ukládal hash tokenu; tady log nahrazuje e-mail.
- **Chyby:** service vrací `NewsletterResult` (kód + hláška pro uživatele). Chyby DB (`DbException`, vyčerpaný pool)
  zachytí service, zaloguje a vrátí „Služba je dočasně nedostupná“. Všechny očekávané stavy včetně chyb vrací
  view s HTTP 200 – je to záměr, zadání zakazuje jen 500. POST bez antiforgery tokenu odmítne framework (400).
- **Validace dvakrát:** atributy modelu pro formulář, service znovu (pravidla patří do business vrstvy).
  Limity jsou v `NewsletterLimits`.

## Co jsem vynechal a proč

Odesílání e-mailu (zadání ho nahrazuje logem), resx lokalizaci, styly, klientskou validaci, integrační testy repository
(procedury ověřuje skript scénářů na skutečném SQL Serveru) a ochranu proti zneužití – mimo rozsah 2–4 h.

## Položky kontrolního seznamu

Mimo rozsah BE: A, C, D, E, I. J1: místo vývojového prostředí jsem funkci ověřil proti SQL Serveru 2022 v Dockeru
skriptem scénářů; tři šířky a tmavý režim se BE netýkají. F2, G1 a G7 uvádí hodnoticí list, v seznamu v1.0 nejsou; chápu je jako DI, tabulku s indexem
a procedurami a skript s hlavičkou.
