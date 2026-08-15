---
id: mp-artifact-frontend-dashboard-workflow
kind: fact
title: Frontend Static Dashboard Workflow
scope: project
status: approved-by-request
created_at: 2026-07-12
updated_at: 2026-08-15
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
- `Logger.UpdateHarvestSummaryUnsafe` writes `${AppDir}/frontend/data/events_summary.json`.
- `Logger.UpdateGlobalStats` writes `${AppDir}/frontend/global_stats.json`.
- `Logger.PushToGit` stages `global_stats.json` and `data/`, commits with a session-specific message, and pushes to the configured data branch.

## Publication Repository Setup

- `Logger.PreparePublicationRepository` runs at startup in the background.
- If `frontend` is not a Git repository, it initializes one.
- If origin is missing, it derives a remote from `STATIC_SITE_REMOTE_URL`, `GITHUB_REMOTE_URL`, `GITHUB_REPOSITORY`, or `GITHUB_REPO` plus `GITHUB_USER`.
- It fetches and checks out the configured data branch when available, otherwise creates the branch locally.
- It removes legacy telemetry files and reinitializes invalid `global_stats.json` or `events_summary.json`.

## Dashboard UI

- `frontend-main/index.html` is a static dashboard titled `FunnyPot SSH Intelligence Dashboard`.
- It fetches from `https://raw.githubusercontent.com/FelixKras/FunnyPot.ai/data`.
- It first loads `global_stats.json` and `data/events_summary.json`, then hydrates detailed views from `data/events.jsonl`.
- It falls back to legacy `data/harvest_summary.json` and `data/harvest.jsonl` if the newer files are unavailable.
- Tabs include Overview, Credentials, Geography, and Attacker Exchange.
- It renders command/result exchanges, MITRE tactics, banners, source IPs, credentials, uploads, high-risk signals, and freshness status through the overview and exchange HUD views.
- Attacker Exchange is a HUD-style exchange matrix showing all command-bearing sessions and matched command/response pairs. It provides derived command volume, reply coverage, risk load, tooling signals, peak UTC activity, behavior summaries, source search, behavior/engagement/risk filters, and sorting by recency, commands, transcript length, duration, risk, type, or remote endpoint.
- The Attacker Exchange HUD includes a dark Mercator-projection SVG world map with labeled origin countries, hover-brightened country markers, and an ocean-placed honeypot anchor. Coordinates come from the existing IP geolocation fallback chain; unresolved sources remain unplotted rather than being assigned fabricated locations. GPU/miner reconnaissance is classified as `GPU prospector` and is available as a behavior filter and map signal. Other dashboard spotlight sections continue to rank sessions by risk and command volume.
- Credentials displays the top 50 usernames and passwords by observed frequency.

## Failure Modes

- Publication is skipped when `GITHUB_TOKEN` or `GITHUB_USER` is missing.
- Publication fails if no static dashboard remote can be configured.
- Invalid publication JSON is reinitialized before commit.
- If detailed event data is unavailable, the dashboard still renders summary metrics and reports details as unavailable.
- The data harvesting requirements document still references older `harvest.jsonl` names; runtime and dashboard currently prefer `events.jsonl` names with legacy fallback.
