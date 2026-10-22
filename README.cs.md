# Registrace k odběru novinek (ASP.NET Core MVC + SQL Server)

English: [README.md](README.md)

## Co to je

Praktický úkol BE pro pozici Vývojář e-shopu POLO3. Malá aplikace v ASP.NET Core MVC (.NET 10), která registruje
e-mail k odběru novinek, registraci potvrdí jednorázovým tokenem a umožní odhlášení. Odeslání e-mailu nahrazuje
zápis do logu s adresou a odkazy.

| Akce | Chování |
|---|---|
| `GET /newsletter` | formulář (e-mail, nepovinné jméno, souhlas) |
| `POST /newsletter/subscribe` | uloží e-mail ve stavu „čeká na potvrzení“ s náhodným tokenem a zaloguje potvrzovací odkaz; opakovaná registrace stejného e-mailu (v jakékoli velikosti písmen) nikdy nevytvoří druhý řádek |
| `GET /newsletter/confirm/{token}` | aktivuje odběr a token zneplatní; neznámý nebo použitý token vrátí hlášku, ne chybu |
| `GET /newsletter/unsubscribe/{token}` | odhlásí; opakované volání je neškodné |

Průvodní text s rozhodnutími a tím, co jsem vynechal: [PRUVODNI_TEXT.md](PRUVODNI_TEXT.md).

## Požadavky

- Rychlé spuštění: Docker s Compose v2
- Vývoj lokálně: .NET 10 SDK (vyvíjeno na 10.0.113) a SQL Server 2019+ (lokální instance nebo kontejner `db`)
- `sqlcmd` pro SQL skript a scénáře; `bash` a `curl` pro scénáře

## Rychlé spuštění (vše v Dockeru)

```bash
cp .env.example .env              # nastavte MSSQL_SA_PASSWORD (min. 8 znaků, velká/malá písmena, číslice)
docker compose up --build         # SQL Server, databáze ze skriptu db/NewsletterSubscription.sql, aplikace
```

Otevřete <http://localhost:5080/newsletter>. `db-init` po startu SQL Serveru vytvoří `NewsletterTest` a spustí SQL skript
klasickým `sqlcmd`; `web` se spustí až po jeho úspěchu. Logy včetně potvrzovacích a odhlašovacích odkazů:
`docker compose logs -f web`. Zastavení: `Ctrl+C`, pak `docker compose down`.

Scénáře proti tomuto prostředí (SQL Server je dostupný na `localhost:1433`):

```bash
SQLCMDPASSWORD=<heslo z .env> scripts/run-scenarios.sh
```

Nedostupná databáze: `docker compose stop db` za běhu `web`, odešlete formulář, pak `docker compose start db`.

## Vývoj lokálně (dotnet run, Docker jen pro SQL Server)

```bash
cp .env.example .env                      # v .env nastavte MSSQL_SA_PASSWORD (min. 8 znaků, velká/malá písmena, číslice)
export SQLCMDPASSWORD=<stejné heslo jako v .env>
docker compose up -d --wait db
sqlcmd -S localhost -U sa -C -Q "CREATE DATABASE NewsletterTest"
sqlcmd -S localhost -U sa -C -d NewsletterTest -b -i db/NewsletterSubscription.sql
export ConnectionStrings__Newsletter="Server=localhost;Database=NewsletterTest;User Id=sa;Password=$SQLCMDPASSWORD;TrustServerCertificate=true;Connect Timeout=5"
dotnet test
dotnet run --project src/Polo3.Newsletter.Web &   # http://localhost:5080/newsletter
scripts/run-scenarios.sh
```

Zastavení: ukončete `dotnet run` a spusťte `docker compose down`. Na Windows s lokálním SQL Serverem
stačí výchozí připojovací řetězec a `sqlcmd -E` (viz níže).

## Připojovací řetězec

Výchozí v `src/Polo3.Newsletter.Web/appsettings.json` (Windows, integrované přihlášení):

```
Server=localhost;Database=NewsletterTest;Integrated Security=true;TrustServerCertificate=true;Connect Timeout=5
```

Jiný server nebo přihlášení SQL nastavte proměnnou prostředí (heslo se do repozitáře neukládá):

```bash
export ConnectionStrings__Newsletter="Server=localhost;Database=NewsletterTest;User Id=sa;Password=<heslo>;TrustServerCertificate=true;Connect Timeout=5"
```

## Spuštění aplikace

```bash
dotnet run --project src/Polo3.Newsletter.Web
```

Aplikace běží na <http://localhost:5080/newsletter>. Potvrzovací e-mail je řádek v logu konzole:
`Newsletter confirmation email for <adresa>: confirm <odkaz>, unsubscribe <odkaz>`.

## Testy a kontroly

```bash
dotnet build -warnaserror
dotnet test
dotnet format --verify-no-changes
```

Jednotkové testy pokrývají service vrstvu s falešným repository v paměti: platná a opakovaná registrace
(jiná velikost písmen, mezery kolem adresy), neplatný e-mail, chybějící souhlas, příliš dlouhé jméno, neznámý,
poškozený a použitý token, dvojí odhlášení, výpadek databáze.

## SQL skript

`db/NewsletterSubscription.sql` vytvoří tabulku `NewsletterSubscription`, její unikátní indexy, uložené procedury
`SyncNewsletterSubscriptionData`, `ConfirmNewsletterSubscription`, `UnsubscribeNewsletterSubscription` a dva testovací
řádky. Počítá s existující prázdnou databází `NewsletterTest` a lze ho spustit opakovaně.

```bash
sqlcmd -S localhost -E -C -Q "CREATE DATABASE NewsletterTest"
sqlcmd -S localhost -E -C -d NewsletterTest -b -i db/NewsletterSubscription.sql
```

S přihlášením SQL (např. Docker) místo `-E` použijte `-U sa` a heslo v proměnné `SQLCMDPASSWORD`.
Skript sám nastavuje `QUOTED_IDENTIFIER ON`, funguje tedy s klasickým i novým `sqlcmd` (ověřeno oběma).

Testovací řádky: `seed.active@example.com` (aktivní, odkaz k odhlášení
`/newsletter/unsubscribe/SeedActiveUnsubscribeToken00000000000000001`) a `seed.unsubscribed@example.com` (odhlášený).

## Scénáře

S běžící aplikací a databází:

```bash
SQLCMDPASSWORD=<heslo> scripts/run-scenarios.sh http://localhost:5080
```

Projde registraci, opakovanou registraci s jinou velikostí písmen, potvrzení, opakované potvrzení, odhlášení,
opakované odhlášení, neznámý token, neplatný vstup, POST bez antiforgery tokenu a 10 souběžných registrací
stejné adresy. Vypíše HTTP kód každého kroku a počet řádků; při chybě skončí nenulovým kódem.

Nedostupná databáze: zastavte SQL Server (`docker compose stop db`) a odešlete formulář. Stránka vrátí
HTTP 200 s hláškou „Služba je dočasně nedostupná“ (`data-result-code="ServiceUnavailable"`), výjimka je v logu.

Vlastní skript: `POST /newsletter/subscribe` vyžaduje antiforgery token. Nejdřív načtěte `GET /newsletter`
se zapnutými cookies, z formuláře vezměte `__RequestVerificationToken` a pošlete ho spolu s cookie
(pole `Email`, `Name`, `IsConsent=true`). Výsledek je ve stránce v atributu `data-result-code`.

## Struktura projektu

| Cesta | Obsah |
|---|---|
| `src/Polo3.Newsletter.Web/Controllers` | `NewsletterController`: validace vstupu, volání service, výběr view |
| `src/Polo3.Newsletter.Web/Services` | `NewsletterService`: logika, tokeny, ošetření chyb DB; `NewsletterResult`, texty hlášek |
| `src/Polo3.Newsletter.Web/Repositories` | `NewsletterSubscriptionRepository`: volání uložených procedur (Dapper) |
| `db/NewsletterSubscription.sql` | tabulka, indexy, 3 procedury, 2 testovací řádky |
| `tests/Polo3.Newsletter.Tests` | jednotkové testy service vrstvy s falešným repository |
| `scripts/run-scenarios.sh` | scénáře ze zadání (curl + sqlcmd) |
| `Dockerfile`, `docker-compose.yml` | rychlé spuštění: `db` (SQL Server), `db-init` (spustí SQL skript), `web` (aplikace, bez roota) |

## Rozhodnutí

Ochrana proti duplicitám, práce s tokeny, ošetření chyb, co jsem vynechal a které položky kontrolního seznamu
se úkolu netýkají, popisuje průvodní text [PRUVODNI_TEXT.md](PRUVODNI_TEXT.md).
