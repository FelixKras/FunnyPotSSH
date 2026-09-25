---
id: mp-artifact-maintenance-workflow
kind: procedure
title: Memory Plane Maintenance Workflow
scope: project
status: approved-by-request
created_at: 2026-07-12
author: Forge
updated_at: 2026-09-25
reviewer: Kilo
source_refs:
  - .memory-plane/README.md
  - .memory-plane/policy.md
  - AGENTS.md
  - memory-plane status (2026-09-25: three SQLite-approved records)
  - git ls-files .memory-plane/artifacts (2026-09-25: twenty tracked Markdown artifacts before this review)
---

# Memory Plane Maintenance Workflow

## Start Work

1. Read `.memory-plane/README.md` and `.memory-plane/policy.md`.
2. Read relevant approved artifacts in `.memory-plane/artifacts/`.
3. Use projections only for navigation or retrieval support.
4. Search the Markdown artifacts as well as `memory-plane retrieve`: the CLI database currently indexes only three newer records, while legacy approved Markdown artifacts remain canonical and searchable by file.

## During Work

Capture durable outcomes as small Markdown files with frontmatter. Use `artifacts/` for approved facts, procedures, and decisions. Use `proposals/` for claims that need review.

Every item should include `id`, `kind`, `title`, `scope`, `status`, `created_at`, `author`, and `source_refs`.

## After Code Changes

Update relevant Memory Plane artifacts when runtime behavior changes. The Memory Plane facts are only as fresh as their cited source files.
