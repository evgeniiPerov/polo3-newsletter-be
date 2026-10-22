# Newsletter subscription (ASP.NET Core MVC + SQL Server)

Česky: [README.cs.md](README.cs.md)

## What it is

Back-end practical task for the POLO3 e-shop developer role. A small ASP.NET Core MVC app (.NET 10) that
subscribes an email to a newsletter, confirms the subscription with a one-time token and lets the user unsubscribe.
Sending the email is replaced by a log line with the address and the links.

| Action | Behaviour |
|---|---|
| `GET /newsletter` | subscription form (email, optional name, consent) |
| `POST /newsletter/subscribe` | stores the email as "waiting for confirmation" with a random token and logs the confirmation link; a repeated subscribe of the same email (any letter case) never creates a second row |
| `GET /newsletter/confirm/{token}` | activates the subscription and invalidates the token; an unknown or used token shows a message, not an error |
| `GET /newsletter/unsubscribe/{token}` | unsubscribes; calling it again is harmless |

Cover text with the decisions and what was skipped (Czech, as the assignment asks): [PRUVODNI_TEXT.md](PRUVODNI_TEXT.md).

## Requirements

- Quick run: Docker with Compose v2
- Local development: .NET 10 SDK (developed on 10.0.113) and SQL Server 2019+ (a local instance or the `db` container)
- `sqlcmd` for the SQL script and the scenarios; `bash` and `curl` for the scenarios

## Quick run (everything in Docker)

```bash
cp .env.example .env              # set MSSQL_SA_PASSWORD (min. 8 chars, upper + lower case, digit)
docker compose up --build         # SQL Server, database from db/NewsletterSubscription.sql, app
```

Open <http://localhost:5080/newsletter>. `db-init` creates `NewsletterTest` and runs the SQL script with the classic
`sqlcmd` once SQL Server is healthy; `web` starts after it succeeds. Logs, including the confirmation and unsubscribe
links: `docker compose logs -f web`. Stop: `Ctrl+C`, then `docker compose down`.

Scenarios against this stack (SQL Server is published on `localhost:1433`):

```bash
SQLCMDPASSWORD=<password from .env> scripts/run-scenarios.sh
```

Database down: `docker compose stop db` while `web` runs, submit the form, then `docker compose start db`.

## Develop locally (dotnet run, Docker only for SQL Server)

```bash
cp .env.example .env                      # set MSSQL_SA_PASSWORD in .env (min. 8 chars, upper + lower case, digit)
export SQLCMDPASSWORD=<same password as in .env>
docker compose up -d --wait db
sqlcmd -S localhost -U sa -C -Q "IF DB_ID('NewsletterTest') IS NULL CREATE DATABASE NewsletterTest"
sqlcmd -S localhost -U sa -C -d NewsletterTest -b -i db/NewsletterSubscription.sql
export ConnectionStrings__Newsletter="Server=localhost;Database=NewsletterTest;User Id=sa;Password=$SQLCMDPASSWORD;TrustServerCertificate=true;Connect Timeout=5"
dotnet test
dotnet run --project src/Polo3.Newsletter.Web &   # http://localhost:5080/newsletter
scripts/run-scenarios.sh
```

Stop: end `dotnet run` and run `docker compose down`. On Windows with a local SQL Server the default connection
string and `sqlcmd -E` are enough (see below).

### Windows (PowerShell)

```powershell
# only if not using the default connection string (integrated security, local SQL Server):
$env:ConnectionStrings__Newsletter = "Server=localhost;Database=NewsletterTest;User Id=sa;Password=<password>;TrustServerCertificate=true;Connect Timeout=5"
dotnet test
dotnet run --project src/Polo3.Newsletter.Web
```

The scenario script needs Git Bash or WSL.

## Connection string

Default in `src/Polo3.Newsletter.Web/appsettings.json` (Windows, integrated security):

```
Server=localhost;Database=NewsletterTest;Integrated Security=true;TrustServerCertificate=true;Connect Timeout=5
```

For another server or SQL login, set an environment variable (no password is stored in the repository):

```bash
export ConnectionStrings__Newsletter="Server=localhost;Database=NewsletterTest;User Id=sa;Password=<password>;TrustServerCertificate=true;Connect Timeout=5"
```

## Run the app

```bash
dotnet run --project src/Polo3.Newsletter.Web
```

The app listens on <http://localhost:5080/newsletter>. The confirmation email is a console log line:
`Newsletter confirmation email for <address>: confirm <link>, unsubscribe <link>`.

## Tests and checks

```bash
dotnet build
dotnet test
dotnet format --verify-no-changes
```

39 tests: 28 unit tests of the service layer with an in-memory fake repository (valid and repeated subscribe in
any letter case, already active email, invalid email, missing consent, too long name, unknown, malformed and used
token, unsubscribing twice, database failure, pool timeout, cancelled request) and 11 HTTP tests through
`WebApplicationFactory` with the same fake repository (routes, antiforgery 400, `data-result-code`, HTTP 200 for
expected outcomes).

## SQL script

`db/NewsletterSubscription.sql` creates the table `NewsletterSubscription`, its unique indexes, the stored procedures
`SyncNewsletterSubscriptionData`, `ConfirmNewsletterSubscription`, `UnsubscribeNewsletterSubscription` and two test rows.
It expects an existing empty database `NewsletterTest` and can be run repeatedly.

```bash
sqlcmd -S localhost -E -C -Q "IF DB_ID('NewsletterTest') IS NULL CREATE DATABASE NewsletterTest"
sqlcmd -S localhost -E -C -d NewsletterTest -b -i db/NewsletterSubscription.sql
```

With SQL login (e.g. Docker) use `-U sa` instead of `-E` and the password in `SQLCMDPASSWORD`.
The script sets `QUOTED_IDENTIFIER ON` itself, so it works with both the classic and the new `sqlcmd` (tested with both).

Test rows: `seed.active@example.com` (active, unsubscribe link
`/newsletter/unsubscribe/SeedActiveUnsubscribeToken00000000000000001`) and `seed.unsubscribed@example.com` (unsubscribed).

## Scenarios

With the app and the database running:

```bash
SQLCMDPASSWORD=<password> scripts/run-scenarios.sh http://localhost:5080
```

The script uses `sqlcmd -U sa` by default. For Windows integrated security set `SQLCMDUSER=` (empty) and the
script calls `sqlcmd -E`.

It runs: subscribe, subscribe again with different letter case, confirm, subscribe the active email again (code
`Subscribed`, still one row, status 2), confirm again, unsubscribe, unsubscribe
again, unknown token, invalid input, POST without the antiforgery token and 10 parallel subscribes of one address.
It prints the HTTP status of each step and the row count, and exits non-zero on any failure.

Database down: stop SQL Server (`docker compose stop db`) and submit the form. The page returns HTTP 200 with
"Služba je dočasně nedostupná" (`data-result-code="ServiceUnavailable"`) and the exception is in the log.

Your own script: `POST /newsletter/subscribe` needs the antiforgery token. First load `GET /newsletter` with cookies
enabled, take `__RequestVerificationToken` from the form and send it with the cookie (fields `Email`, `Name`,
`IsConsent=true`). The outcome is in the page's `data-result-code` attribute.

## Project layout

| Path | Content |
|---|---|
| `src/Polo3.Newsletter.Web/Controllers` | `NewsletterController`: input validation, calls the service, picks the view |
| `src/Polo3.Newsletter.Web/Services` | `NewsletterService`: rules, tokens, database error handling; `NewsletterResult`, user messages |
| `src/Polo3.Newsletter.Web/Repositories` | `NewsletterSubscriptionRepository`: stored procedure calls (Dapper) |
| `db/NewsletterSubscription.sql` | table, indexes, 3 procedures, 2 test rows |
| `tests/Polo3.Newsletter.Tests` | service layer unit tests and HTTP tests (`WebApplicationFactory`) with a fake repository |
| `scripts/run-scenarios.sh` | assignment scenarios (curl + sqlcmd) |
| `Dockerfile`, `docker-compose.yml` | quick run: `db` (SQL Server), `db-init` (runs the SQL script), `web` (the app, non-root) |

## Decisions

Duplicate prevention, token handling, error handling, what was skipped and which checklist items do not apply
are described in the cover text [PRUVODNI_TEXT.md](PRUVODNI_TEXT.md) (Czech).
