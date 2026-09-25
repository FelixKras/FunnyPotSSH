---
id: "mp-20260925-010138a951"
kind: "procedure"
title: "Dashboard session models have conditional timeline fallbacks"
scope: "project"
status: "approved"
created_at: "2026-09-25T06:00:43.648126Z"
author: "Kilo"
confidence: 0.98
approved_at: "2026-09-25T06:00:43.658184Z"
reviewer: "Kilo"
supersedes: "mp-20260904-19e04decc9"
source_refs:
  - "frontend-main/index.html:1395-1488; FunnyPot/Program.cs:3384-3415"
tags:
  - "frontend"
  - "telemetry"
---

# Dashboard session models have conditional timeline fallbacks

The dashboard loads structured sessions and threat intelligence. A present prebuilt Timeline is mapped as supplied; when absent, fallback synthesis includes session start, auth, command, result, and session end but not shell start/end. Live session objects are persisted on session_end and flushed at publication; interrupted sessions may remain incomplete. Optional sessions.json failure permits summaries, whereas missing required global_stats.json or both summary sources yields a loading error.
