---
id: "mp-20261126-b5bf5d776f"
kind: "episode"
title: "Attacker Exchange caught-binary display, session search, and late payload-capture merge"
scope: "project"
status: "approved"
created_at: "2026-11-26T16:27:47.990168Z"
author: "Antigravity"
confidence: 1.0
approved_at: "2026-11-26T16:27:47.999548Z"
reviewer: "Antigravity"
source_refs:
  - "frontend-main/index.html; FunnyPot/Program.cs; FunnyPot.Tests/UnitTests.cs; scripts/test-ollama-frontend.mjs"
tags:
  - "frontend"
  - "malware"
  - "telemetry"
---

# Attacker Exchange caught-binary display, session search, and late payload-capture merge

Fixed three gaps preventing caught binary details from appearing in Attacker Exchange (investigated via hafih@77.239.124.73:10746 / 035e6d1ae3bb40ff and e2a7bfc1df3e43c4): (1) frontend-main/index.html now correlates real analyzed binaries from data/binaries.json, session.uploads, and session.timeline and renders both a session-level Caught Binary Artifacts panel and inline per-step callouts in renderLatestExchanges; (2) exchangeSearchMatches now indexes session IDs, filenames, SHA-256 hashes, and indicators (fixing the Caught Binaries jump button) while filteredExchangeSessions retains 0-command binary-bearing sessions; (3) FunnyPot/Program.cs adds Logger.MergeStructuredSession so late async payload_capture events arriving after session_end merge into existing sessions.json entries instead of overwriting commands. Verified with 200/200 .NET tests, scripts/test-ollama-frontend.mjs, and live published data smoke check. Pending commit/push and container redeploy.
