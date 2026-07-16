---
id: mp-artifact-architecture-map
kind: fact
title: FunnyPot Architecture Map
scope: project
status: approved-by-request
created_at: 2026-07-12
author: Forge
source_refs:
  - FunnyPot/Program.cs
  - FunnyPot/AppConfiguration.cs
  - FunnyPot/FakeFileSystem.cs
  - FunnyPot/TelemetryWriteQueue.cs
  - FunnyPot/AutoResearchRunner.cs
  - config/app-config.yaml
  - docker-compose.yaml
  - docs/DEPLOY.md
---

# FunnyPot Architecture Map

FunnyPot is a Docker-hosted SSH honeypot. The runtime accepts SSH connections, records authentication attempts, opens interactive or exec shells, resolves attacker commands through local emulation, static responses, cache, or OpenRouter-backed LLM responses, then publishes structured telemetry for dashboard use.

## Runtime Components

- `Program`: process entry point, runtime configuration loading, SSH server lifecycle, connection limits, banner rotation, signal handling, OpenRouter startup check, and AutoResearch mode dispatch.
- `SshServer` integration: FxSsh server bound to the configured SSH port, with `UserauthService` and `ConnectionService` handlers registered per session.
- `SetupUserauth`: password authentication and credential harvesting flow, including configured credential acceptance and harvest-threshold acceptance.
- `SetupShell`: shell and exec channel lifecycle, prompt handling, idle timeout, command queue, command analysis, response resolution, result logging, and publication trigger.
- `CommandResolver`: input classification and response selection for built-ins, static responses, SCP commands, cached command responses, local fallback, and LLM calls.
- `FakeFileSystem`: per-shell simulated filesystem state, seeded Linux paths, synthetic file content, and stateful local mutations.
- `DataHarvester`: command analytics for discovery, payload URLs, egress targets, persistence, tunneling, persona breakout attempts, MITRE tactics, and payload metadata capture.
- `Logger`: structured JSONL telemetry writer, summary/stat updater, app log writer, and static dashboard Git publisher.
- `TelemetryWriteQueue`: bounded background write queue for non-blocking telemetry writes.
- `AutoResearchRunner`: optional experiment loop used when the app is launched with `--autoresearch`.

## Runtime Flow

1. Load `.env`, YAML config, and environment overrides.
2. Generate or load an SSH host key, start the FxSsh server, and optionally rotate banners.
3. On connection, allocate a connection session key, enforce `MAX_SESSIONS`, and log `session_start`.
4. On authentication, log `auth_attempt`, harvest passwords, and accept either configured credentials or the configured harvest threshold.
5. On shell or exec channel open, allocate a shell session ID, create a per-session fake filesystem, start a dedicated command worker, and log `shell_session_start`.
6. For each command, analyze behavior, validate input, resolve a response, log input and output telemetry, send terminal output, and close exec sessions after completion.
7. On shell close, update global stats, remove per-session state, and request static dashboard publication.

## Boundaries

- External SSH traffic enters through the Docker-published SSH port, default `22722`.
- LLM traffic uses OpenRouter through the configured API base URL and chat endpoint.
- Runtime telemetry is local JSONL and JSON under `/var/log/funnypot` and `frontend/data`.
- Dashboard publication uses a Git repository under the app `frontend` directory and pushes to the configured data branch.
- The static UI lives separately in `frontend-main/index.html` and consumes retained data from the published data branch.

## Operational Notes

- `scripts/deploy.sh` is the canonical deployment path.
- `run-docker.sh` is a local helper that starts Compose and sends a bring-up SSH auth probe.
- `docs/DEPLOY.md` warns that cold start can take 2-3 minutes before the SSH listener binds.
