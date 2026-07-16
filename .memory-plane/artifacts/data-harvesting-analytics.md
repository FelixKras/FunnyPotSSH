---
id: mp-artifact-data-harvesting-analytics
kind: fact
title: Data Harvesting And Analytics
scope: project
status: approved-by-request
created_at: 2026-07-12
author: Forge
source_refs:
  - FunnyPot/Program.cs
  - FunnyPot.Tests/UnitTests.cs
  - docs/data-harvesting-requirements.md
---

# Data Harvesting And Analytics

FunnyPot derives attacker-behavior telemetry at command time and records both raw events and aggregate summaries for dashboard presentation.

## Command Analysis

`DataHarvester.AnalyzeCommand` computes:

- `DiscoveryDepthScore` for sensitive discovery targets such as `/etc/passwd`, `.ssh`, `authorized_keys`, `id_rsa`, `.env`, `/etc/shadow`, secrets, Kubernetes config, and standard discovery commands.
- `PersistenceVector` for systemd, cron, shell profile, and SSH authorized-keys persistence.
- `PayloadUrls` from `http`, `https`, `ftp`, and `tftp` URLs.
- `EgressTargets` from commands using `nc`, `netcat`, `curl`, `wget`, `ssh`, and `telnet`.
- `TunnelingIntent` for SSH dynamic, local, and remote forwarding, plus proxy tools such as `frp`, `chisel`, and `socat`.
- `PersonaBreakoutAttempt` for prompt-injection and LLM-disclosure phrasing.
- `ReconnaissanceProbe` for MikroTik RouterOS-style command probes.
- `SemanticComplexity` from token count and shell operator count.
- `AssetValuePerceptionScore` from discovery, payload, egress, persistence, and tunneling intensity.
- `MitreAttackTechniques` as broad tactics: Execution, Persistence, Discovery, Command and Control, and Reconnaissance.

## Session Analytics

- `ShellSessionAnalytics` tracks total commands, failed commands, first and last semantic complexity, MITRE technique counts, standard-error ratio, semantic drift, and Turing multiplier.
- `CommandSequenceLatencyMs` measures delay between command completion and next command start.
- Latency under 50 ms is labeled `automation` in command telemetry.
- `TimeToCompromiseMs` is logged when a shell or exec channel opens.

## Payload Metadata Capture

- Each payload URL is captured asynchronously so attacker interaction is not blocked.
- Only `http` and `https` payloads are fetched; other schemes are logged as unsupported.
- Payload capture streams response bytes, computes SHA-256, stops at 10 MiB, and logs statuses such as `captured`, `http_error`, `too_large`, `unsupported_scheme`, or `error`.

## Credential Analytics

- Authentication logging computes password entropy.
- Sequential credential mutation is measured with Levenshtein distance from the previous credential in the same connection session.
- Fingerprint hash normalizes client version, key algorithm, and fingerprint material into a SHA-256 hash when available.

## Aggregates

- `ApplyHarvestSummaryEvent` updates event counts, scan attempts, unique scan IPs, top usernames, top passwords, and shell totals.
- `UpdateGlobalStats` aggregates session count, command count, blocked operations, tokens, durations, users, banners, MITRE distribution, and mean engagement.
- Summary maps are capped at 1000 entries; top users in global stats are capped to 10.

## Coverage Status

- Current implementation covers many requirements from `docs/data-harvesting-requirements.md`.
- ASN, proxy, VPN, Tor, and object-storage archival requirements are described in requirements but are not implemented in the observed code.
