---
id: "mp-20261112-47cbfe65f7"
kind: "decision"
title: "Shared hourly SSH and Ollama data publication without session-end bypasses"
scope: "project"
status: "approved"
created_at: "2026-11-12T05:51:23.359488Z"
author: "Forge"
confidence: 1.0
approved_at: "2026-11-12T05:51:23.426189Z"
reviewer: "Forge"
source_refs:
  - "FunnyPot/Program.cs"
  - "FunnyPot/AppConfiguration.cs"
  - "config/app-config.yaml"
  - "docker-compose.yaml"
  - "FunnyPot.Tests/UnitTests.cs"
tags:
  - "publication"
  - "scheduling"
---

# Shared hourly SSH and Ollama data publication without session-end bypasses

User selected hourly publication on 2026-11-12. Set DATA_PUSH_INTERVAL_SECONDS default and YAML/C# defaults to 3600. Replaced Ollama-only timer with an unconditional single shared timer for SSH and Ollama, including when API listener is disabled. Removed all event-triggered requests, forced boundary pushes and direct shell-finalization Git pushes. Startup recovery remains an explicit exception after restarts. Local telemetry capture is immediate; routine dashboard publication may lag up to an hour. Verified only runtime call sites remaining are startup recovery and scheduled-publication. Effective Compose setting is 3600; 152 .NET tests and frontend checks pass, including delayed first tick and cancellation tests. Deployment pending after this checkpoint.
