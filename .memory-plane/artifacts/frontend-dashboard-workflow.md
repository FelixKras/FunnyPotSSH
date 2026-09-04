---
id: mp-artifact-frontend-dashboard-workflow
kind: fact
title: Frontend Static Dashboard Workflow
scope: project
status: approved-by-request
created_at: 2026-07-12
updated_at: 2026-09-04
author: Forge
source_refs:
  - FunnyPot/Program.cs
  - frontend-main/index.html
  - docs/docker-secrets.md
  - docs/data-harvesting-requirements.md
  - scripts/deploy.sh
---

# Frontend Static Dashboard Workflow

FunnyPot separates dashboard UI from published telemetry data. Runtime writes data into an app-local `frontend` repository and publishes it to a configured data branch; `frontend-main/index.html` consumes retained JSON and JSONL from that branch.

## Runtime Publication Data

- `Logger.LogHarvestUnsafe` writes publishable telemetry to `${AppDir}/frontend/data/events.jsonl`.
- `Logger.UpdateThreatIntelUnsafe` maintains persistent harvested intelligence in `${AppDir}/frontend/data/threat_intel.json`.
- `Logger.UpdateActiveSessionTimelineUnsafe` maintains in-memory active session timelines and emits bounded structured debriefs (`${AppDir}/frontend/data/sessions.json`, max 2,000 sessions configured via `STRUCTURED_SESSION_LIMIT`).
- Summary and stats are maintained incrementally:
  - `${AppDir}/frontend/data/events_summary.json` (exact active stream event tallies)
  - `${AppDir}/frontend/global_stats.json` (exact active stream metrics)
  - `${AppDir}/frontend/data/sessions.json` (bounded, pre-assembled, timed, queryable session debriefs with inferred tactical objectives, command latency, and MITRE techniques)
- `Logger.PushToGit` stages `global_stats.json` and `data/`, commits with a session-specific message, and pushes to the configured data branch. A local snapshot safeguards against race conditions with concurrent telemetry writes during git fetch/checkout.

## Publication Repository Setup

- `Logger.PreparePublicationRepository` runs at startup synchronously prior to starting the SSH listener.
- If `frontend` is not a Git repository, it initializes one.
- If origin is missing, it derives a remote from `STATIC_SITE_REMOTE_URL`, `GITHUB_REMOTE_URL`, `GITHUB_REPOSITORY`, or `GITHUB_REPO` plus `GITHUB_USER`.
- It fetches and checks out the configured data branch when available, otherwise creates the branch locally.
- To avoid unbounded startup delays on long-running deployments, full stream replay on startup is disabled; one-time manual recalculation can be invoked via `--rebuild-publication-data`.

## Dashboard UI

- `frontend-main/index.html` is a static dashboard titled `FunnyPot SSH Intelligence Dashboard`.
- It fetches from `https://raw.githubusercontent.com/FelixKras/FunnyPot.ai/data` with cache-busting timestamps.
- Initial load concurrently fetches `global_stats.json`, `data/events_summary.json`, `data/threat_intel.json`, and `data/sessions.json`.
- Browser-side `events.jsonl` hydration has been removed to prevent client-side memory exhaustion and payload bloat; session models map directly from pre-assembled debriefs in `data/sessions.json`.
- If `data/sessions.json` is available, session models map directly from pre-assembled debriefs, synthesizing full chronological timelines (`session_start`, `auth_attempt`, `shell_session_start`, `command`, `command_result`, `session_end`) with defensive property fallbacks and precomputed `publishedRiskScore`.
- Tabs include Overview, Credentials, Geography, and Attacker Exchange.
- The Overview tab features a dedicated **Top Attacker Commands & Tactical Objectives** component, classifying observed commands with inferred strategic goals (e.g., privilege identification, kernel reconnaissance, payload ingress, cron persistence) and MITRE ATT&CK tags.
- Credentials and Geography tabs consume `threat_intel.json` to present the complete historical threat intelligence dataset (usernames, passwords, scanner source IPs) across resets.
- It renders command/result exchanges, MITRE tactics, banners, source IPs, credentials, uploads, high-risk signals, and freshness status through the overview and exchange HUD views.
- Attacker Exchange is a HUD-style exchange matrix showing all command-bearing sessions and matched command/response pairs. It provides derived command volume, reply coverage, risk load, tooling signals, peak UTC activity, behavior summaries, normalized search across commands/responses and session metadata, behavior/engagement/risk filters, and sorting by recency, commands, transcript length, duration, risk, type, or remote endpoint.
- Session views derive timing-based interaction profiles from time-to-first-command, per-command attacker response latency, rapid-reply share, and cadence variability. Profiles are labeled script-like, agent-like, human-like, mixed/uncertain, or insufficient-data and are explicitly estimates rather than verified identities.
- Shell Opens and Shell Closes remain separate lifecycle counters so operators can detect active channels or missing end telemetry. The overview no longer promotes blocked-operation, text-command, or upload-count/byte totals as headline metrics; detailed upload events remain available within session evidence.
- The Overview tab opens with a marketing-oriented project introduction and a local animated attack-flow visualization (`frontend-main/assets/funnypot-attack-flow.gif`) showing the path from SSH probe through synthetic shell interaction to retained threat intelligence. Its synthetic-shell stage includes a smiling “mischief bot” injecting playful chaos into the terminal to communicate FunnyPot's deliberate attacker-confusion element.
- The Attacker Exchange HUD includes a dark Mercator-projection SVG world map with labeled origin countries, hover-brightened country markers, and an ocean-placed honeypot anchor. Coordinates come from the existing IP geolocation fallback chain; unresolved sources remain unplotted rather than being assigned fabricated locations. GPU/miner reconnaissance is classified as `GPU prospector` and is available as a behavior filter and map signal. Other dashboard spotlight sections continue to rank sessions by risk and command volume.
- Credentials displays the top 50 usernames and passwords by observed frequency.

## Failure Modes

- Publication is skipped when `GITHUB_TOKEN` or `GITHUB_USER` is missing.
- Publication fails if no static dashboard remote can be configured.
- Invalid publication JSON is reinitialized before commit.
- If detailed event data is unavailable, the dashboard still renders summary metrics and reports details as unavailable.
- The data harvesting requirements document still references older `harvest.jsonl` names; runtime and dashboard currently prefer `events.jsonl` names with legacy fallback.
