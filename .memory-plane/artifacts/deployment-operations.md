---
id: mp-artifact-deployment-operations
kind: procedure
title: Deployment And Operations
scope: project
status: approved-by-request
created_at: 2026-07-12
author: Forge
source_refs:
  - docs/DEPLOY.md
  - docker-compose.yaml
  - run-docker.sh
  - scripts/deploy.sh
  - FunnyPot/Program.cs
---

# Deployment And Operations

FunnyPot runs as a single Docker Compose service named `funnypot`, with container name `funnypot-container`.

## Topology

- Image is built from the repository `Dockerfile` through Compose.
- Default exposed SSH honeypot port is `22722`, configurable with `SSH_PORT`.
- Restart policy is `unless-stopped`.
- Container security settings include `no-new-privileges:true` and dropping all capabilities.
- Runtime log directory inside the container is `/var/log/funnypot`.
- Application directory inside the container is `/home/test/app`.

## Deployment Paths

- Canonical path: `scripts/deploy.sh`.
- Local manual path: `run-docker.sh`.
- Raw fallback: `docker compose up -d --build`, but this skips checks and the local bring-up probe.

## Canonical Deploy Script

- `scripts/deploy.sh` resolves the repo root, chooses Docker Compose v2 or v1, checks for unfinished merge or rebase state, commits and pushes changed `frontend-main` and `frontend` submodules when present, commits and pushes parent repository changes when present, then rebuilds and starts Compose.
- Because it can commit and push, inspect working tree state before using it when manual control is needed.

## Local Run Script

- `run-docker.sh` requires `.env` to exist.
- It runs Compose build/start and sends a best-effort SSH auth probe to `test@127.0.0.1` on port `22722` using askpass.
- It prints a manual test command for SSH access after startup.

## Verification

- Use `docker ps -a` rather than host `ps` to identify the running instance.
- Health should eventually report `healthy` for `funnypot-container`.
- The SSH banner can be checked with a short TCP read from localhost port `22722`.
- During cold start, check `/proc/1/net/tcp` inside the container for a listening row ending in hex port `58D2`.

## Logs And Recovery

- `docker logs funnypot-container` may be quiet during normal operation because app logs are written under `/var/log/funnypot` and `frontend/sessions`.
- Use `docker exec funnypot-container ls /var/log/funnypot/` to inspect runtime logs.
- Common lifecycle commands are `docker compose stop`, `start`, `restart`, `down`, and `up -d --build`.

## Known Operational Pitfalls

- A fresh image can take 2-3 minutes before the SSH port binds.
- Early healthcheck connection refusals are expected during cold start.
- Prompt regressions can make the honeypot respond with meta-question fallback text for real commands; this is a prompt/runtime regression, not a Docker issue.
