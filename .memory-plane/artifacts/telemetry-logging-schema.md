---
id: mp-artifact-telemetry-logging-schema
kind: fact
title: Telemetry And Logging Schema
scope: project
status: approved-by-request
created_at: 2026-07-12
author: Forge
source_refs:
  - FunnyPot/Program.cs
  - FunnyPot/TelemetryWriteQueue.cs
  - docs/data-harvesting-requirements.md
  - frontend-main/index.html
---

# Telemetry And Logging Schema

FunnyPot emits structured JSONL telemetry through `Logger.LogYaml`. Despite the method name, the current harvested telemetry format is JSONL records with `Timestamp`, `Event`, and `Data` fields.

## Write Pipeline

- `Logger.LogYaml(eventType, data)` drops private loopback endpoints, then enqueues work on `TelemetryWriteQueue`.
- `TelemetryWriteQueue` uses a bounded `BlockingCollection<Action>` with default capacity 1024 and a background thread named `funnypot-telemetry-writer`.
- If the queue is full or disposed, the event is dropped with an error message to stderr.
- `Logger.Shutdown()` disposes the telemetry queue during application shutdown.

## Storage Outputs

- Hot runtime stream: `${LogDir}/events.jsonl`, default `/var/log/funnypot/events.jsonl`.
- Static dashboard stream: `${AppDir}/frontend/data/events.jsonl` for publishable event types.
- Summary JSON: `${AppDir}/frontend/data/events_summary.json`.
- Global stats JSON: `${AppDir}/frontend/global_stats.json`.
- App logs: `${AppDir}/frontend/sessions/app-<session-or-name>-<date>.log`.
- Telemetry files are deleted and restarted if appending would exceed `TELEMETRY_MAX_BYTES`, default 50 MiB with a 1 KiB minimum.

## Published Event Types

- `session_start`
- `session_end`
- `auth_attempt`
- `harvested_credential`
- `shell_session_start`
- `shell_session_end`
- `command`
- `command_result`
- `payload_capture`
- `scp_upload_captured`
- `scp_upload_rejected`

## Event Data Shapes

- `AuthAttemptLogEntry`: timestamp, session key, remote endpoint, username, auth method, password for password auth, key metadata, attempt number, accepted flag, acceptance reason, entropy, credential distance, and fingerprint hash.
- `HarvestedCredential`: timestamp, username, password, session key, remote endpoint, attempt number, and auth method.
- `SessionLogEntry`: timestamp, connection and shell IDs, endpoint, username, client version, banner, event, duration, and time-to-compromise.
- `CommandLogEntry`: message numbers, endpoint, username, exchange ID, command, latency, automation hint, derived DHS analytics, and MITRE tactics.
- `CommandResultLogEntry`: command response, LLM model, response source, failed-command flag, response duration, hallucination feedback, standard-error ratio, semantic drift, and Turing multiplier.
- `PayloadCaptureLogEntry`: payload URL, status, HTTP status, bytes, SHA-256, and error.
- `SCPUploadLogEntry`: filename, byte count, SHA-256, stored path, and status.
- `GlobalStats`: total sessions, commands, blocked operations, token counts, duration, top users, sessions by banner, MITRE distribution, mean engagement, and last updated time.
- `HarvestSummary`: event counts, scan attempts, unique source IPs, shell count, top usernames, top passwords, and scans by IP.

## Dashboard Assumptions

- `frontend-main/index.html` reads `global_stats.json`, `data/events_summary.json`, and `data/events.jsonl` from the published data branch.
- The dashboard retains compatibility fallback paths for older `harvest.jsonl` and `harvest_summary.json` files.
