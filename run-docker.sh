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

echo "Waiting for health check..."
for i in $(seq 1 12); do
  status=$(docker inspect --format='{{.State.Health.Status}}' funnypot-container 2>/dev/null)
  if [ "$status" = "healthy" ]; then
    echo "Container is healthy."
    break
  fi
  sleep 5
done

docker ps --filter name=funnypot-container --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
echo "Done. SSH to: ssh -p 22722 test@localhost"
