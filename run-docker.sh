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

echo "Done. Give it a few seconds then: ssh -p 22722 test@localhost"
