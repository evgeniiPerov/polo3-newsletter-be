# Registrace k odběru novinek (ASP.NET Core MVC + SQL Server)

Praktický úkol BE pro pozici Vývojář e-shopu POLO3. Průvodní text: [PRUVODNI_TEXT.md](PRUVODNI_TEXT.md).

## Struktura

| Cesta | Obsah |
|---|---|
| `src/Polo3.Newsletter.Web/Controllers` | `NewsletterController` – validace vstupu, volání service, výběr view |
| `src/Polo3.Newsletter.Web/Services` | `NewsletterService` – logika, tokeny, ošetření chyb DB; `NewsletterResult`, texty hlášek |
| `src/Polo3.Newsletter.Web/Repositories` | `NewsletterSubscriptionRepository` – volání uložených procedur (Dapper) |
| `db/NewsletterSubscription.sql` | tabulka, indexy, 3 procedury, 2 testovací řádky |
| `tests/Polo3.Newsletter.Tests` | jednotkové testy service vrstvy s falešným repository |
| `scripts/run-scenarios.sh` | scénáře ze zadání (curl + sqlcmd) |

## Požadavky

- .NET 10 SDK (vyvíjeno na 10.0.113)
- SQL Server 2019+ (lokální instance, nebo Docker: zkopírujte `.env.example` do `.env`, nastavte silné heslo SA
  a spusťte `docker compose up -d --wait`)
- `sqlcmd` (pro skript databáze a scénáře), `curl` a bash (pro scénáře)

## Rychlý start (Docker, Linux/macOS)

```bash
cp .env.example .env                      # v .env nastavte MSSQL_SA_PASSWORD (min. 8 znaků, velká/malá písmena, číslice)
export SQLCMDPASSWORD=<stejné heslo jako v .env>
docker compose up -d --wait
sqlcmd -S localhost -U sa -C -Q "CREATE DATABASE NewsletterTest"
sqlcmd -S localhost -U sa -C -d NewsletterTest -b -i db/NewsletterSubscription.sql
export ConnectionStrings__Newsletter="Server=localhost;Database=NewsletterTest;User Id=sa;Password=$SQLCMDPASSWORD;TrustServerCertificate=true;Connect Timeout=5"
dotnet test
dotnet run --project src/Polo3.Newsletter.Web &   # http://localhost:5080/newsletter
scripts/run-scenarios.sh
```

Zastavení: ukončete `dotnet run` a spusťte `docker compose down`. Na Windows s lokálním SQL Serverem
stačí výchozí připojovací řetězec a `sqlcmd -E` (viz níže).

## Databáze

Skript počítá s existující prázdnou databází `NewsletterTest` a lze ho spustit opakovaně.

```bash
sqlcmd -S localhost -E -C -Q "CREATE DATABASE NewsletterTest"
sqlcmd -S localhost -E -C -d NewsletterTest -b -i db/NewsletterSubscription.sql
```

S přihlášením SQL (např. Docker) místo `-E` použijte `-U sa` a heslo v proměnné `SQLCMDPASSWORD`.
Skript sám nastavuje `QUOTED_IDENTIFIER ON`, funguje tedy s klasickým i novým `sqlcmd` (ověřeno oběma).

Testovací řádky: `seed.active@example.com` (aktivní, odhlášení
`/newsletter/unsubscribe/SeedActiveUnsubscribeToken00000000000000001`) a `seed.unsubscribed@example.com` (odhlášený).

## Připojovací řetězec

Výchozí v `src/Polo3.Newsletter.Web/appsettings.json` (Windows, integrované přihlášení):

```
Server=localhost;Database=NewsletterTest;Integrated Security=true;TrustServerCertificate=true;Connect Timeout=5
```

Jiný server nebo přihlášení SQL nastavte proměnnou prostředí (heslo se do repozitáře neukládá):

```bash
export ConnectionStrings__Newsletter="Server=localhost;Database=NewsletterTest;User Id=sa;Password=<heslo>;TrustServerCertificate=true;Connect Timeout=5"
```

## Spuštění

```bash
dotnet run --project src/Polo3.Newsletter.Web
```

Aplikace běží na <http://localhost:5080/newsletter>. Odeslání e-mailu nahrazuje zápis do logu konzole:
`Newsletter confirmation email for <adresa>: confirm <odkaz>, unsubscribe <odkaz>`.

## Testy a kontroly

```bash
dotnet build -warnaserror
dotnet test
dotnet format --verify-no-changes
```

## Scénáře

S běžící aplikací a databází:

```bash
SQLCMDPASSWORD=<heslo> scripts/run-scenarios.sh http://localhost:5080
```

Projde registraci, opakovanou registraci s jinou velikostí písmen, potvrzení, opakované potvrzení, odhlášení,
opakované odhlášení, neznámý token, neplatný vstup, POST bez antiforgery tokenu a 10 souběžných registrací
stejné adresy. Vypíše HTTP kód každého kroku a počet řádků; při chybě skončí nenulovým kódem.

Nedostupná databáze: zastavte SQL Server (`docker compose stop mssql`) a odešlete formulář – stránka vrátí
HTTP 200 s hláškou „Služba je dočasně nedostupná“ (`data-result-code="ServiceUnavailable"`), výjimka je v logu.

Vlastní skript: `POST /newsletter/subscribe` vyžaduje antiforgery token. Nejdřív načtěte `GET /newsletter`
se zapnutými cookies, z formuláře vezměte `__RequestVerificationToken` a pošlete ho spolu s cookie
(pole `Email`, `Name`, `IsConsent=true`). Výsledek je ve stránce v atributu `data-result-code`.
