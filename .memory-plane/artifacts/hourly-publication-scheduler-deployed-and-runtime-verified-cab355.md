---
id: "mp-20261112-172dcab355"
kind: "episode"
title: "Hourly publication scheduler deployed and runtime verified"
scope: "project"
status: "approved"
created_at: "2026-11-12T05:53:03.720120Z"
author: "Forge"
confidence: 1.0
approved_at: "2026-11-12T05:53:03.728606Z"
reviewer: "Forge"
source_refs:
  - "FunnyPot/Program.cs"
  - "FunnyPot.Tests/UnitTests.cs"
  - "docker exec printenv DATA_PUSH_INTERVAL_SECONDS: 3600"
  - "docker compose ps: healthy"
tags:
  - "publication"
  - "deployment"
---

# Hourly publication scheduler deployed and runtime verified

Deployed shared hourly SSH/Ollama publication scheduler with docker compose up -d --build funnypot on 2026-11-12. Live DATA_PUSH_INTERVAL_SECONDS is 3600, container healthy, API on host 11433 responds, and remote data tip remained unchanged during a 60-second post-start verification window despite normal service operation and API probe. All event/boundary/direct shell-end publication call sites removed; only startup recovery and one shared timer call PushToGit. Timer runs regardless of Ollama enabled state, waits a complete interval before first periodic publish and cancels cleanly. Local recording remains immediate. Startup recovery remains an explicit additional publication on restart. Verified 152 .NET tests and frontend checks; no code commit/push performed. Full real one-hour cycle was not waited during this task; scheduler timing and cancellation were covered in tests.
