#!/usr/bin/env bash
# End-to-end scenarios from the assignment against a running app and its database.
# Usage: scripts/run-scenarios.sh [baseUrl]   (default http://localhost:5080)
# Needs curl and sqlcmd. Database access: SQLCMDSERVER (default localhost), SQLCMDUSER (default sa;
# set it empty, SQLCMDUSER=, for integrated security), SQLCMDPASSWORD, SQLCMDDBNAME (default NewsletterTest).
set -euo pipefail

BASE_URL=${1:-http://localhost:5080}
TEMP_DIR=$(mktemp -d)
trap 'rm -rf "$TEMP_DIR"' EXIT

FAILED=0
SAW_500=0
EMAIL=Scenario.Test@Example.com
RACE_EMAIL=scenario.race@example.com
RACE_REQUESTS=10

# Empty but set SQLCMDUSER selects integrated security (-E); unset means the SQL login sa.
sql() {
    local auth=(-U "${SQLCMDUSER-sa}")
    [ -z "${SQLCMDUSER-sa}" ] && auth=(-E)
    sqlcmd -S "${SQLCMDSERVER:-localhost}" "${auth[@]}" -C -d "${SQLCMDDBNAME:-NewsletterTest}" \
        -h -1 -W -b -Q "SET NOCOUNT ON; $1" | tr -d '[:space:]'
}

row_count() {
    sql "SELECT COUNT(*) FROM dbo.NewsletterSubscription WHERE Email = N'$1';"
}

# Prints the HTTP status; the body goes to the file in $1.
get() {
    curl -s -o "$1" -w '%{http_code}' "$BASE_URL$2"
}

# Loads the form for a fresh antiforgery cookie + token, then posts it.
# Arguments: body file, email, consent (true/false). Prints the HTTP status.
post_subscribe() {
    local body=$1 jar
    jar=$(mktemp -p "$TEMP_DIR")
    local token
    token=$(curl -s -c "$jar" "$BASE_URL/newsletter" |
        sed -n 's/.*name="__RequestVerificationToken" type="hidden" value="\([^"]*\)".*/\1/p')
    curl -s -b "$jar" -o "$body" -w '%{http_code}' \
        --data-urlencode "Email=$2" \
        --data-urlencode "IsConsent=$3" \
        --data-urlencode "__RequestVerificationToken=$token" \
        "$BASE_URL/newsletter/subscribe"
}

# Arguments: scenario name, expected status, actual status, body file, expected text in body (optional).
expect() {
    local name=$1 expected=$2 actual=$3 body=$4 pattern=${5:-}
    [ "$actual" = 500 ] && SAW_500=1
    if [ "$actual" = "$expected" ] && { [ -z "$pattern" ] || grep -q -- "$pattern" "$body"; }; then
        printf 'PASS  %-55s HTTP %s\n' "$name" "$actual"
    else
        printf 'FAIL  %-55s HTTP %s (expected %s%s)\n' "$name" "$actual" "$expected" "${pattern:+, body with $pattern}"
        FAILED=$((FAILED + 1))
    fi
}

expect_equal() {
    if [ "$2" = "$3" ]; then
        printf 'PASS  %-55s %s\n' "$1" "$3"
    else
        printf 'FAIL  %-55s %s (expected %s)\n' "$1" "$3" "$2"
        FAILED=$((FAILED + 1))
    fi
}

for _ in $(seq 1 30); do
    curl -sf -o /dev/null "$BASE_URL/newsletter" && break
    sleep 1
done
curl -sf -o /dev/null "$BASE_URL/newsletter" || { echo "App is not answering on $BASE_URL"; exit 1; }

body="$TEMP_DIR/body.html"
sql "DELETE FROM dbo.NewsletterSubscription WHERE Email LIKE N'scenario%@example.com';" >/dev/null

code=$(post_subscribe "$body" "$EMAIL" true)
expect "1. subscribe $EMAIL" 200 "$code" "$body" 'data-result-code="Subscribed"'

code=$(post_subscribe "$body" "${EMAIL,,}" true)
expect "2. subscribe again, other letter case" 200 "$code" "$body" 'data-result-code="Subscribed"'
expect_equal "2. rows for the email" 1 "$(row_count "$EMAIL")"

confirm_token=$(sql "SELECT ConfirmToken FROM dbo.NewsletterSubscription WHERE Email = N'$EMAIL';")
unsubscribe_token=$(sql "SELECT UnsubscribeToken FROM dbo.NewsletterSubscription WHERE Email = N'$EMAIL';")

code=$(get "$body" "/newsletter/confirm/$confirm_token")
expect "3. confirm" 200 "$code" "$body" 'data-result-code="Confirmed"'

code=$(post_subscribe "$body" "$EMAIL" true)
expect "3b. subscribe the active email again" 200 "$code" "$body" 'data-result-code="Subscribed"'
expect_equal "3b. rows for the active email" 1 "$(row_count "$EMAIL")"
expect_equal "3b. status stays active (2)" 2 "$(sql "SELECT Status FROM dbo.NewsletterSubscription WHERE Email = N'$EMAIL';")"

code=$(get "$body" "/newsletter/confirm/$confirm_token")
expect "4. confirm again with the used token" 200 "$code" "$body" 'data-result-code="TokenInvalid"'

code=$(get "$body" "/newsletter/unsubscribe/$unsubscribe_token")
expect "5. unsubscribe" 200 "$code" "$body" 'data-result-code="Unsubscribed"'

code=$(get "$body" "/newsletter/unsubscribe/$unsubscribe_token")
expect "6. unsubscribe again" 200 "$code" "$body" 'data-result-code="Unsubscribed"'

unknown_token=$(head -c 32 /dev/urandom | base64 | tr '+/' '-_' | tr -d '=' | cut -c1-43)
code=$(get "$body" "/newsletter/confirm/$unknown_token")
expect "7. confirm with an unknown token" 200 "$code" "$body" 'data-result-code="TokenInvalid"'
code=$(get "$body" "/newsletter/unsubscribe/$unknown_token")
expect "7. unsubscribe with an unknown token" 200 "$code" "$body" 'data-result-code="TokenInvalid"'
code=$(get "$body" "/newsletter/confirm/abc%20def")
expect "7. confirm with a malformed token" 200 "$code" "$body" 'data-result-code="TokenInvalid"'

code=$(post_subscribe "$body" "scenario-not-an-email" true)
expect "8. subscribe with an invalid email" 200 "$code" "$body" 'data-result-code="InvalidInput"'
code=$(post_subscribe "$body" "scenario.noconsent@example.com" false)
expect "8. subscribe without consent" 200 "$code" "$body" 'data-result-code="InvalidInput"'
expect_equal "8. rows created by invalid input" 0 "$(row_count scenario.noconsent@example.com)"

code=$(curl -s -o "$body" -w '%{http_code}' --data "Email=$EMAIL&IsConsent=true" "$BASE_URL/newsletter/subscribe")
expect "9. post without antiforgery token" 400 "$code" "$body"

for i in $(seq 1 "$RACE_REQUESTS"); do
    post_subscribe "$TEMP_DIR/race-$i.html" "$RACE_EMAIL" true >"$TEMP_DIR/race-$i.code" &
done
wait
for i in $(seq 1 "$RACE_REQUESTS"); do
    expect "10. parallel subscribe #$i" 200 "$(cat "$TEMP_DIR/race-$i.code")" "$TEMP_DIR/race-$i.html" 'data-result-code="Subscribed"'
done
expect_equal "10. rows after $RACE_REQUESTS parallel subscribes" 1 "$(row_count "$RACE_EMAIL")"

echo "Scenario rows in table: $(sql "SELECT COUNT(*) FROM dbo.NewsletterSubscription WHERE Email LIKE N'scenario%@example.com';")"
echo "FAILED: $FAILED"
if [ "$SAW_500" = 1 ]; then
    echo "At least one response was HTTP 500."
    exit 1
fi
[ "$FAILED" = 0 ]
