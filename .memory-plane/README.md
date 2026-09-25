---
id: mp-readme
kind: procedure
title: FunnyPot Memory Plane
scope: project
status: approved-by-request
created_at: 2026-07-12
author: Forge
updated_at: 2026-09-25
reviewer: Kilo
source_refs:
  - AGENTS.md
  - memory-plane status (2026-09-25: three SQLite-approved records)
  - git ls-files .memory-plane/artifacts (2026-09-25: twenty tracked Markdown artifacts before this review)
---

# FunnyPot Memory Plane

This directory is the canonical long-horizon memory plane for FunnyPot.

Durable project knowledge belongs here as reviewed artifacts and proposals, not in transient chat context.

## Layout

- `artifacts/`: accepted durable project memory, procedures, decisions, and stable facts.
- `proposals/`: unapproved claims, summaries, and candidates that need human or curator review.
- `projections/`: generated indexes derived from source artifacts.
- `events.jsonl`: append-only audit trail for memory-plane maintenance.

## Maintenance

1. Start substantial work by reading this file and relevant `artifacts/`.
2. Use `proposals/` for claims that are not yet approved.
3. Update artifacts when runtime behavior changes to keep facts fresh.
4. Read Markdown artifacts directly for legacy `approved-by-request` records: the SQLite-backed `memory-plane retrieve` index currently contains only CLI-created records and does not enumerate all approved Markdown artifacts. Do not infer that an absent search result means no approved artifact exists.
