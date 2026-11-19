---
id: "mp-20261112-ac4f13d602"
kind: "task"
title: "Compact data branch history in batches of one hundred"
scope: "project"
status: "approved"
created_at: "2026-11-12T05:10:41.331886Z"
author: "Forge"
confidence: 1.0
approved_at: "2026-11-12T05:10:41.340801Z"
reviewer: "Forge"
source_refs:
  - "git rev-list --count data in isolated clone: 16195"
  - "FunnyPot/Program.cs"
tags:
  - "publication"
  - "history"
---

# Compact data branch history in batches of one hundred

User requested data-branch history compaction on 2026-11-12 after confirming router forwarding. Complete isolated bare filtered clone at /tmp/kilo/funnypot-data-compact-20261112.git shows 16195 linear commits (no merges) before ongoing new publications. Host frontend checkout is stale and has an unrelated local ahead commit; do not reset or rewrite it. Production publisher has a shallow checkout in its named volume and syncs remote before each push. Intended operation: stop publisher briefly, fetch final remote data tip, preserve remote backup branch, create a new linear snapshot commit for each oldest-to-newest batch of 100 old commits plus final remainder, verify final tree byte-identical, update only data with an explicit lease, resume production and verify normal publication. Main frontend branch must remain unchanged.
