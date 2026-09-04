---
id: mp-20260904-3d30f75c0e
kind: decision
title: Tiered Threat Intelligence and Session Publication Architecture
scope: project
status: approved
created_at: 2026-09-04T12:19:25.148055Z
author: opencode
confidence: 1.0
approved_at: 2026-09-04T12:19:25.148055Z
reviewer: opencode
source_refs:
  - FunnyPot/Program.cs:2720,FunnyPot/Program.cs:3130,frontend-main/index.html:1420
tags:
  - telemetry
  - threat-intel
  - sessions
---

# Tiered Threat Intelligence and Session Publication Architecture

Implemented a three-tier publication architecture: (1) data/threat_intel.json curated at runtime preserving all 453+ usernames, 916+ passwords, and 288+ scanner IPs; (2) data/events_summary.json and global_stats.json recalculated on each push from the post-Sep-1 events.jsonl baseline; (3) data/sessions.json pre-assembling timed, structured debriefable session objects with inferred command objectives and MITRE techniques.
