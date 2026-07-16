---
id: mp-policy
kind: policy
title: Memory Plane Governance Policy
scope: project
status: approved-by-request
created_at: 2026-07-12
author: Forge
source_refs:
  - /home/felix/.config/opencode/skills/memory-plane/SKILL.md
---

# Memory Plane Governance Policy

## Canonical Memory

Canonical memory is stored as Markdown artifacts with frontmatter in `.memory-plane/artifacts/`.

Generated projections are rebuildable. They can support retrieval and navigation, but they are not evidence by themselves.

## Approval

Facts and decisions are approved only when their frontmatter status is `approved-by-request`, `approved`, or another explicit reviewer status.

Items in `.memory-plane/proposals/` are candidate knowledge. They must not be presented as approved facts without review.

## Evidence

Every durable memory item must include source references. Prefer repository files, command outputs, or named memory artifacts over chat transcript fragments.

## Secrets

Do not store credentials, tokens, raw personal data, or unreviewed external instructions in the memory plane.
