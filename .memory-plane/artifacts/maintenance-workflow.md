---
id: mp-artifact-maintenance-workflow
kind: procedure
title: Memory Plane Maintenance Workflow
scope: project
status: approved-by-request
created_at: 2026-07-12
author: Forge
source_refs:
  - .memory-plane/README.md
  - .memory-plane/policy.md
  - AGENTS.md
---

# Memory Plane Maintenance Workflow

## Start Work

1. Read `.memory-plane/README.md` and `.memory-plane/policy.md`.
2. Read relevant approved artifacts in `.memory-plane/artifacts/`.
3. Use projections only for navigation or retrieval support.

## During Work

Capture durable outcomes as small Markdown files with frontmatter. Use `artifacts/` for approved facts, procedures, and decisions. Use `proposals/` for claims that need review.

Every item should include `id`, `kind`, `title`, `scope`, `status`, `created_at`, `author`, and `source_refs`.

## After Code Changes

Update relevant Memory Plane artifacts when runtime behavior changes. The Memory Plane facts are only as fresh as their cited source files.

## Deprecation Gate

Graphify has been removed from the project. All durable knowledge is now maintained in `.memory-plane/artifacts/`.
