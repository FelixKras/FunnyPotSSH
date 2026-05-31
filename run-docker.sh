#!/bin/bash
set -e

# Use docker compose (v2) or docker-compose (v1)
COMPOSE="docker compose"
if ! docker compose version &>/dev/null; then
  COMPOSE="docker-compose"
fi

# Ensure .env exists
if [ ! -f .env ]; then
  echo "Error: .env file not found. Create one with at least OPENROUTER_API_KEY=sk-or-v1-..."
  exit 1
fi

echo "Building and starting..."
$COMPOSE up -d --build

echo "Running bring-up SSH auth probe..."
if command -v ssh >/dev/null 2>&1 && command -v timeout >/dev/null 2>&1 && command -v setsid >/dev/null 2>&1; then
  ASKPASS_FILE="$(mktemp /tmp/funnypot-askpass.XXXXXX)"
  KNOWN_HOSTS_FILE="$(mktemp /tmp/funnypot-known-hosts.XXXXXX)"
  trap 'rm -f "$ASKPASS_FILE" "$KNOWN_HOSTS_FILE"' EXIT
  cat > "$ASKPASS_FILE" <<'EOF'
#!/bin/sh
printf '%s\n' test
EOF
  chmod 700 "$ASKPASS_FILE"
  timeout 10s env DISPLAY=:1 SSH_ASKPASS_REQUIRE=force SSH_ASKPASS="$ASKPASS_FILE" \
    setsid ssh -p 22722 \
      -o StrictHostKeyChecking=no \
      -o UserKnownHostsFile="$KNOWN_HOSTS_FILE" \
      -o PreferredAuthentications=password \
      -o PubkeyAuthentication=no \
      test@127.0.0.1 'exit' >/dev/null 2>&1 || true
  echo "Bring-up auth probe sent."
else
  echo "Skipping bring-up auth probe: ssh, timeout, or setsid is unavailable."
fi

echo "Done. FunnyPot listens on port 22722. Manual test: ssh -p 22722 test@localhost"
