---
id: mp-artifact-cached-response-quality-session-2026-07-16
kind: decision
title: Cached Response Quality And Telemetry Reset Session
scope: project
status: approved-by-request
created_at: 2026-07-16
author: Forge
source_refs:
  - FunnyPot/Program.cs
  - FunnyPot/FakeFileSystem.cs
  - FunnyPot.Tests/UnitTests.cs
  - frontend-main/index.html
  - frontend/data/events.jsonl
  - Telegram conversation with project owner on 2026-07-16
---

# Cached Response Quality And Telemetry Reset Session

## Message Exchange And Decisions

1. The project owner asked whether cached attacker responses were functional. Inspection confirmed that `CommandResponseCache` loads prior `command_result` telemetry, serves exact normalized command matches before OpenRouter, and stores new eligible responses.
2. Runtime telemetry contained 6,009 cacheable command-result records representing 221 unique command keys at inspection time.
3. A deterministic random sample of ten responses exposed inconsistent CPU counts, incomplete password interactions, and incorrect pipeline behavior.
4. The project owner asked whether a command/response frequency map existed. No persisted map existed; frequencies were derived from raw telemetry.
5. At the owner's request, the 50 most frequent responses and then all remaining observed command families were repaired with deterministic local routing, a coherent synthetic host profile, and stricter cache eligibility checks.
6. The dashboard gained an explicit amber `cached response` badge while keeping cache details hidden from SSH attackers.
7. The owner then approved resetting collected attacker-response telemetry locally and remotely while retaining the cache implementation.
8. After a live compound fingerprint command incorrectly returned only the nested `nproc` value `2`, the owner approved routing compound commands through the LLM with structured host facts, expected-output labels, a worked example, and one validation-repair retry.

## Durable Outcome

- Cache functionality remains enabled in `CommandResponseCache`.
- Password-changing and unstable sudo-password probes are not cached.
- Known high-frequency probes are handled locally before stale cache entries.
- CPU, memory, disk, GPU, operating-system, uptime, process, and network responses share one synthetic host profile.
- The dashboard identifies telemetry records whose `ResponseSource` is `cache`.
- Collected telemetry is reset to an empty baseline; future attacker activity repopulates it normally.
- The generalized implementation passed 185 tests before deployment.
- Compound commands use an exact whole-command cache match only when its response satisfies the extracted output-label structure. Otherwise they bypass local observed-response routing and use structure-guided LLM prompting.

## Privacy

This artifact intentionally excludes raw attacker credentials, endpoints, payloads, and command secrets.
