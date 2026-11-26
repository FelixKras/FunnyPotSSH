---
id: mp-artifact-current-tasks-open-risks
kind: task
title: Current Tasks And Open Risks
scope: project
status: approved-by-request
created_at: 2026-07-12
author: Forge
updated_at: 2026-11-26
reviewer: Antigravity
source_refs:
  - .memory-plane/artifacts/architecture-map.md
  - .memory-plane/artifacts/maintenance-workflow.md
  - .memory-plane/artifacts/frontend-dashboard-workflow.md
  - docker-compose.yaml
  - FunnyPot/Program.cs
  - frontend-main/index.html
  - frontend-main/404.html
  - memory-plane status (2026-11-26)
---

# Current Tasks And Open Risks

The Memory Plane contains approved-by-request coverage for all major knowledge areas.

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

## Open Risks

- Some requirements in `docs/data-harvesting-requirements.md` are aspirational or partially implemented, including ASN/proxy profiling and cold archival storage.
- Static dashboard data naming has evolved from `harvest.*` to `events.*`; dashboard fallbacks exist, but documentation may still mention older names.
- Deployment script can commit and push repository changes; use it carefully in dirty worktrees.
- Live LLM and end-to-end SSH behavior are not fully covered by observed unit tests.
- Memory Plane facts are only as fresh as their cited source files; update artifacts when runtime behavior changes.
- Live deployment inspection on 2026-09-24 found `MAX_SESSIONS=1` despite the Compose default of 50; the single slot rejected concurrent connections. Recheck the container environment before treating this observation as a current setting. The new readiness probe avoids taking that slot but does not increase capacity.
- Incremental publication aggregates may diverge from the retained event stream after rotation or interruption; full recalculation is manual (`--rebuild-publication-data`), not automatic on every push.
- SQLite retrieval currently indexes only CLI-created records, not the legacy approved Markdown artifacts. Consult both until indexing is reconciled.
- `command_responses.json` can learn exact attacker input containing password candidates; the live working copy contains such keys. Never stage newly learned raw secret-bearing entries for publication without a redaction policy. Review the staged diff, not merely the working-tree diff, before pushing.
- Working-tree updates from 2026-11-26 (Attacker Exchange caught-binary display, per-tab clean URLs + `404.html`, Attack Mind Map pilot tab in `frontend-main`, and `Logger.MergeStructuredSession` in `FunnyPot/Program.cs`) are unit-tested locally but not yet committed/pushed to GitHub Pages or deployed to the running container.
- Historical `sessions.json` entries already overwritten by late `payload_capture` events prior to `Logger.MergeStructuredSession` (e.g., `e2a7bfc1df3e43c4`) remain 0-command binary-only sessions unless rebuilt via `--rebuild-publication-data`; the frontend now renders them cleanly in Attacker Exchange and Attack Mind Map.
