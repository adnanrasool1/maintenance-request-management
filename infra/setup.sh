#!/usr/bin/env bash
# Creates infra/.env from .env.example with random secrets. Never overwrites an existing .env.
set -eu

cd "$(dirname "$0")"

if [ -f .env ]; then
  echo "infra/.env already exists; leaving it unchanged."
  exit 0
fi

# Prints $2 random characters from the tr character set $1.
rand_chars() {
  LC_ALL=C tr -dc "$1" </dev/urandom | head -c "$2"
}

# 24 characters that always meet SQL Server complexity (upper, lower, digit, symbol).
# The symbols avoid characters that break .env files, shells or connection strings.
gen_password() {
  printf '%s%s%s%s%s' \
    "$(rand_chars 'A-Za-z0-9' 20)" \
    "$(rand_chars 'A-Z' 1)" \
    "$(rand_chars 'a-z' 1)" \
    "$(rand_chars '0-9' 1)" \
    "$(rand_chars '._!-' 1)"
}

# 32 random bytes (256 bits), base64-encoded.
gen_jwt_key() {
  if command -v openssl >/dev/null 2>&1; then
    openssl rand -base64 32 | tr -d '\r\n'
  else
    head -c 32 /dev/urandom | base64 | tr -d '\r\n'
  fi
}

tmp=".env.tmp.$$"
trap 'rm -f "$tmp"' EXIT

while IFS= read -r line || [ -n "$line" ]; do
  line=${line%$'\r'}
  case "$line" in
    MSSQL_SA_PASSWORD=change-me)     echo "MSSQL_SA_PASSWORD=$(gen_password)" ;;
    MRA_APP_PASSWORD=change-me)      echo "MRA_APP_PASSWORD=$(gen_password)" ;;
    SYSTEM_ADMIN_PASSWORD=change-me) echo "SYSTEM_ADMIN_PASSWORD=$(gen_password)" ;;
    JWT_SIGNING_KEY=change-me)       echo "JWT_SIGNING_KEY=$(gen_jwt_key)" ;;
    *)                               printf '%s\n' "$line" ;;
  esac
done < .env.example > "$tmp"

if grep -q '=change-me$' "$tmp"; then
  echo "setup.sh: a placeholder in .env.example has no generator; aborting." >&2
  exit 1
fi

mv "$tmp" .env
echo "Created infra/.env with random secrets."
echo "System Admin credentials: SYSTEM_ADMIN_EMAIL and SYSTEM_ADMIN_PASSWORD in infra/.env"
