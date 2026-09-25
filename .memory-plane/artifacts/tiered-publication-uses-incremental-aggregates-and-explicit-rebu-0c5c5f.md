---
id: "mp-20260925-e60e0c5c5f"
kind: "decision"
title: "Tiered publication uses incremental aggregates and explicit rebuild"
scope: "project"
status: "approved"
created_at: "2026-09-25T06:00:36.261936Z"
author: "Kilo"
confidence: 0.98
approved_at: "2026-09-25T06:00:36.323020Z"
reviewer: "Kilo"
supersedes: "mp-20260904-3d30f75c0e"
source_refs:
  - "FunnyPot/Program.cs:2783-2808,3059-3123,3268-3372; docker-compose.yaml:37-47"
tags:
  - "telemetry"
  - "publication"
---

# Tiered publication uses incremental aggregates and explicit rebuild

Runtime curates threat intelligence and structured session debriefs. events_summary.json and global_stats.json are maintained incrementally during events; PushToGit snapshots and publishes but does not replay events.jsonl on each push. A full replay requires --rebuild-publication-data. Stream rotation can leave aggregate counters divergent from the retained event stream. The Compose publication repository resides in the frontend-publication-data volume.
