---
id: mp-artifact-current-tasks-open-risks
kind: task
title: Current Tasks And Open Risks
scope: project
status: approved-by-request
created_at: 2026-07-12
author: Forge
source_refs:
  - .memory-plane/artifacts/architecture-map.md
  - .memory-plane/artifacts/maintenance-workflow.md
---

# Current Tasks And Open Risks

The Memory Plane now contains approved-by-request coverage for all major knowledge areas. Graphify has been removed from the project.

## Completed Migration

- Architecture map artifact added.
- SSH session and attacker interaction flow artifact added.
- LLM response flow artifact added.
- Fake filesystem behavior artifact added.
- Telemetry and logging schema artifact added.
- Data harvesting and analytics artifact added.
- SCP upload handling artifact added.
- Configuration and secrets rules artifact added.
- Deployment and operations artifact added.
- Testing strategy artifact added.
- Frontend/static dashboard workflow artifact added.
- Current tasks and open risks artifact added.
- Graphify removed and data backed up to `backup/graphify-out/`.

## Open Risks

- Some requirements in `docs/data-harvesting-requirements.md` are aspirational or partially implemented, including ASN/proxy profiling and cold archival storage.
- Static dashboard data naming has evolved from `harvest.*` to `events.*`; dashboard fallbacks exist, but documentation may still mention older names.
- Deployment script can commit and push repository changes; use it carefully in dirty worktrees.
- Live LLM and end-to-end SSH behavior are not fully covered by observed unit tests.
- Memory Plane facts are only as fresh as their cited source files; update artifacts when runtime behavior changes.
