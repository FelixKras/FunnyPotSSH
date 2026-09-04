---
id: mp-20260904-c3f850abf1
kind: decision
title: Telemetry Schema Modernization and Clean Baseline Reset
scope: project
status: approved
created_at: 2026-09-04T05:06:27.644106Z
author: opencode
confidence: 1.0
approved_at: 2026-09-04T05:06:27.644106Z
reviewer: opencode
source_refs:
  - FunnyPot/Program.cs:2446,frontend-main/index.html:1400
tags:
  - telemetry
  - schema
  - baseline
---

# Telemetry Schema Modernization and Clean Baseline Reset

Telemetry logging was transitioned to a per-session sequence format on 2026-09-01, introducing top-level SessionId, Sequence, and ExchangeId with lean Data envelopes. Historical pre-reset command logs were cleared to establish a clean telemetry baseline. The frontend index.html parser was updated to combine outer envelope fields with event payload data for proper exchange correlation and rendering. Publication snapshot synchronization was made additive to prevent accidental truncation.
