---
id: mp-proposal-top-command-response-quality
kind: fact
title: Frequent Command Response Quality Routing
scope: project
status: pending-review
created_at: 2026-07-16
author: Forge
source_refs:
  - FunnyPot/Program.cs
  - FunnyPot.Tests/UnitTests.cs
  - /var/log/funnypot/events.jsonl (runtime analysis on 2026-07-16)
---

# Frequent Command Response Quality Routing

Runtime analysis of the 50 most frequent command results found that most problematic cached entries were password-varying forms of the same CPU-count probe. Cached model responses contradicted the synthetic host profile by reporting between 2 and 21 CPUs. Other frequent quality problems included a false `lockr: command not found`, inconsistent CPU models and memory/disk sizes, and an empty response for `ls -lh $(which ls)`.

`CommandResolver.TryGenerateFrequentCommandResponse` now handles these observed command families before cache lookup. It keeps the host profile at two CPUs, returns consistent CPU/GPU/memory/disk details, handles common persistence chains silently while preserving local filesystem mutations, and returns deterministic output for the frequent host-fingerprint probes.

The initial full test suite passed with 170 tests after adding regression coverage for the frequent-response route.

Follow-up coverage generalized this route across all observed telemetry command families. Password-changing pipelines now fail consistently without caching credentials, distribution and host-fingerprint probes share the Debian 6 profile, direct CPU and memory files match `nproc`, `lscpu`, `free`, and `top`, and malformed model-control/truncation output is rejected from the cache.

The generalized implementation passed the full 185-test suite.
