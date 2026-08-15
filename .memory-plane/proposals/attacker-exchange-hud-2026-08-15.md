---
id: mp-proposal-attacker-exchange-hud-2026-08-15
kind: change-note
title: Attacker Exchange HUD Dashboard
scope: frontend-main
status: proposed
created_at: 2026-08-15
author: OpenCode
source_refs:
  - frontend-main/index.html
  - .memory-plane/artifacts/frontend-dashboard-workflow.md
---

# Attacker Exchange HUD Dashboard

The Attacker Exchange tab now presents a HUD-style exchange matrix with derived command volume, reply coverage, risk load, tooling signals, peak UTC activity, behavior summaries, filter controls, sortable sessions, and a coordinate-based SVG source map. Source coordinates are populated through the existing IP geolocation fallback chain and remain absent rather than fabricated when lookup data is unavailable.
