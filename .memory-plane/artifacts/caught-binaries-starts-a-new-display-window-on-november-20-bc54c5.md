---
id: "mp-20261120-54b9bc54c5"
kind: "decision"
title: "Caught Binaries starts a new display window on November 20"
scope: "project"
status: "approved"
created_at: "2026-11-20T12:46:01.151617Z"
author: "Forge"
confidence: 1.0
approved_at: "2026-11-20T12:46:01.160326Z"
reviewer: "Forge"
source_refs:
  - "frontend-main commit 56998f13"
tags:
  - "frontend"
  - "quarantine"
---

# Caught Binaries starts a new display window on November 20

Frontend commit 56998f13 resets the Caught Binaries tab to records at or after 2026-11-20T12:44:31Z from both the published index and session-derived telemetry, removes the old partial-capture report, and counts rejected attempts only within the new window. This is a dashboard reset, not a quarantine or telemetry purge. Frontend and cutoff tests passed.
