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

FunnyPot emits structured JSONL telemetry through `Logger.LogYaml`. Despite the method name, the current harvested telemetry format is JSONL records with top-level `Timestamp`, `Event`, `SessionId`, `Sequence`, optional `ChannelId` and `ExchangeId`, and lean event-specific `Data` fields.

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
- `shell_session_start`
- `shell_session_end`
- `command`
- `command_result`
- `payload_capture`
- `scp_upload_captured`
- `scp_upload_rejected`

## Event Data Shapes

- `AuthAttemptLogEntry`: remote endpoint, username, auth method, password for password auth, connection attempt number, accepted flag, and acceptance reason.
- `SessionLogEntry`: timestamp, connection and shell IDs, endpoint, username, client version, banner, event, duration, and time-to-compromise.
- `CommandLogEntry`: message numbers, endpoint, username, exchange ID, command, latency, automation hint, derived DHS analytics, and MITRE tactics.
- `CommandResultLogEntry`: command response, LLM model, response source, failed-command flag, response duration, hallucination feedback, standard-error ratio, semantic drift, and Turing multiplier.
- `PayloadCaptureLogEntry`: payload URL, status, HTTP status, bytes, SHA-256, and error.
- `SCPUploadLogEntry`: filename, byte count, SHA-256, stored path, and status.
- `GlobalStats`: total sessions, commands, blocked operations, token counts, duration, top users, sessions by banner, MITRE distribution, mean engagement, and last updated time.
- `HarvestSummary`: event counts, scan attempts, unique source IPs, shell count, top usernames, top passwords, and scans by IP.

Events for a session receive monotonically increasing sequence numbers. Sequence state is released when `session_end` is queued or written. Derived command analytics remain available to the in-process summary path and are not repeated in each raw event.

## 2026-09-01 Baseline Reset and Schema Transition

- On 2026-09-01, the telemetry stream was deliberately reset to establish a clean baseline under the new per-session sequence schema (`SessionId`, `Sequence`, `ExchangeId`, `Data`).
- In this schema, `Data` objects are kept lean to minimize log bloat. Top-level envelope properties (`Timestamp`, `Sequence`, `SessionId`, `ExchangeId`) are normalized by the frontend parser in `frontend-main/index.html` into `model.commands` and `model.results` entries.
- To protect newly accumulated records across container lifecycles, publication snapshot restoration (`RestorePublicationSnapshot`) reconciles and appends JSONL lines rather than replacing directory contents, and the startup preparation step synchronizes the remote data branch before the SSH server begins accepting external traffic.

## Dashboard Assumptions

- `frontend-main/index.html` reads `global_stats.json`, `data/events_summary.json`, and `data/events.jsonl` from the published data branch.
- When processing `command` and `command_result` events, the frontend unpacks envelope fields (`Timestamp`, `Sequence`, `ExchangeId`, `SessionId`) together with payload data to maintain full fidelity for timeline sorting, session pair correlation, and exchange metadata rendering.
- The dashboard retains compatibility fallback paths for older `harvest.jsonl` and `harvest_summary.json` files.
