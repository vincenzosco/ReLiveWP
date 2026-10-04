#!/usr/bin/env bash
# Initialises secrets required for local development and Docker Compose.
# Safe to re-run: existing secrets are never overwritten.
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SECRETS_DIR="$ROOT_DIR/deploy/secrets"
IDENTITY_DIR="$ROOT_DIR/src/backend/ReLiveWP.Backend.Identity"

mkdir -p "$SECRETS_DIR"

if [ -f "$SECRETS_DIR/jwt_private_key" ]; then
    echo "JWT private key already exists, skipping generation."
else
    echo "Generating JWT ECDSA P-256 key pair..."

    TMPKEY="$(mktemp)"
    trap 'rm -f "$TMPKEY"' EXIT

    # generate key in traditional EC format so ImportECPrivateKey can consume the DER bytes
    openssl ecparam -name prime256v1 -genkey -noout -out "$TMPKEY" 2>/dev/null
    openssl ec -in "$TMPKEY" -outform DER 2>/dev/null | base64 > "$SECRETS_DIR/jwt_private_key"

    echo "  Written: deploy/secrets/jwt_private_key"
fi

if [ ! -f "$SECRETS_DIR/jwt_public_key" ]; then
    base64 -d "$SECRETS_DIR/jwt_private_key" \
        | openssl ec -inform DER -pubout -outform DER 2>/dev/null \
        | base64 > "$SECRETS_DIR/jwt_public_key"
    echo "  Written: deploy/secrets/jwt_public_key"
fi

if [ ! -f "$SECRETS_DIR/connection_secret_key" ]; then
    openssl rand -base64 32 | tr -d '\n' > "$SECRETS_DIR/connection_secret_key"
    echo "  Written: deploy/secrets/connection_secret_key"
fi

if [ ! -f "$SECRETS_DIR/media_ticket_secret" ]; then
    openssl rand -base64 32 | tr -d '\n' > "$SECRETS_DIR/media_ticket_secret"
    echo "  Written: deploy/secrets/media_ticket_secret"
fi

if [ ! -f "$SECRETS_DIR/passport_sts_key" ]; then
    openssl rand -base64 24 | tr -d '\n' > "$SECRETS_DIR/passport_sts_key"
    echo "  Written: deploy/secrets/passport_sts_key"
fi

PRIVATE_KEY="$(cat "$SECRETS_DIR/jwt_private_key")"
dotnet user-secrets --project "$IDENTITY_DIR" set "JWT:PrivateKey" "$PRIVATE_KEY"
