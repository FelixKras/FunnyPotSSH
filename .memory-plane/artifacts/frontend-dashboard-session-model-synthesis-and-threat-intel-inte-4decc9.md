---
id: mp-20260904-19e04decc9
kind: procedure
title: Frontend Dashboard Session Model Synthesis and Threat Intel Integration
scope: project
status: approved
created_at: 2026-09-04T13:32:10.753868Z
author: opencode
confidence: 1.0
approved_at: 2026-09-04T13:32:10.753868Z
reviewer: opencode
source_refs:
  - frontend-main/index.html:1420,frontend-main/index.html:2895
tags:
  - frontend
  - dashboard
  - telemetry
---

# Frontend Dashboard Session Model Synthesis and Threat Intel Integration

Updated frontend-main/index.html to load data/sessions.json and data/threat_intel.json. When mapping prebuilt sessions, index.html synthesizes complete event timelines (session_start, auth_attempt, shell_session_start, command, command_result, session_end) and implements defensive property access fallbacks on session models to eliminate undefined errors. Credentials, geography, and top commands views consume threat_intel.json to display cumulative harvested intelligence.
