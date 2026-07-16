---
id: mp-artifact-testing-strategy
kind: procedure
title: Testing Strategy
scope: project
status: approved-by-request
created_at: 2026-07-12
author: Forge
source_refs:
  - FunnyPot.Tests/UnitTests.cs
  - FunnyPot.Tests/FunnyPot.Tests.csproj
  - FunnyPot/FunnyPot.csproj
  - config/app-config.yaml
  - docs/DEPLOY.md
---

# Testing Strategy

FunnyPot has a focused xUnit test suite covering input validation, SCP handling, data harvesting, logger aggregation, config loading, command resolution, fake filesystem continuity, AutoResearch, command worker ordering, telemetry queue behavior, and LLM rate limiting.

## Primary Verification Command

```bash
dotnet test FunnyPot.sln
```

## Important Coverage Areas

- `InputValidatorTests`: empty input, max length, null bytes, repetitive characters, normal commands, and Unicode text.
- `SCPDetectorTests`: SCP and SFTP detection without false positives on normal shell commands.
- `SCPUploadSessionTests`: binary upload capture and oversized upload rejection.
- `DataHarvesterTests`: password mutation distance, payload URL extraction, MITRE tactic classification, persistence, tunneling, RouterOS probes, fingerprint hashing, failure response detection, and shell analytics.
- `LoggerTests`: session log naming, data-push debounce behavior, and harvest summary aggregation.
- `ProgramTests`: remote endpoint normalization and integer environment parsing.
- `CommandResolverTests`: executable normalization, old Debian fingerprinting, CPU-info formatting, uptime persistence, terminal-output normalization, system/user prompt contracts, static responses, LLM-vs-local routing, compound command detection, fake filesystem persistence, binary cat output, OpenRouter parsing, and API URL construction.
- `AppConfigurationTests`: missing config fallback, file binding, project config binding, default path loading, and published config availability.
- `TelemetryWriteQueueTests`: queued writes before dispose and rejection after dispose.
- `AutoResearchRunnerTests`: metric parsing, improvement direction, and mutable path confinement.
- `SessionCommandWorkerTests`: ordered work execution on a dedicated worker thread and rejection after dispose.
- `LlmRateLimiterTests`: per-session rate limit isolation.

## Deploy Verification

- After Docker deploy, verify container health and SSH banner as described in `docs/DEPLOY.md`.
- Do not treat the first cold-start healthcheck failures as test failures; startup can take 2-3 minutes.

## Test Gaps To Remember

- The suite is mostly unit-level and does not appear to include an end-to-end SSH client test through FxSsh.
- OpenRouter calls are not exercised as live integration tests in the observed suite.
- Static dashboard rendering is not covered by browser automation in the observed suite.
