---
id: mp-readme
kind: procedure
title: FunnyPot Memory Plane
scope: project
status: approved-by-request
created_at: 2026-07-12
author: Forge
source_refs:
  - AGENTS.md
---

# FunnyPot Memory Plane

This directory is the canonical long-horizon memory plane for FunnyPot.

Durable project knowledge belongs here as reviewed artifacts and proposals, not in transient chat context.

## Layout

- `artifacts/`: accepted durable project memory, procedures, decisions, and stable facts.
- `proposals/`: unapproved claims, summaries, and candidates that need human or curator review.
- `projections/`: generated indexes derived from source artifacts.
- `events.jsonl`: append-only audit trail for memory-plane maintenance.

## Generated Projections

- `projections/hierarchy.json`: tree view of approved artifacts organized by type.
- `projections/community-index.md`: review-friendly table of community names, categories, confidence, and node counts.

## Maintenance

1. Start substantial work by reading this file and relevant `artifacts/`.
2. Use `proposals/` for claims that are not yet approved.
3. Update artifacts when runtime behavior changes to keep facts fresh.
