---
id: "mp-20261112-abaa66b593"
kind: "episode"
title: "Data branch compacted from 16200 updates into 162 checkpoints"
scope: "project"
status: "approved"
created_at: "2026-11-12T05:15:43.785284Z"
author: "Forge"
confidence: 1.0
approved_at: "2026-11-12T05:15:43.793976Z"
reviewer: "Forge"
supersedes: "mp-20261112-ac4f13d602"
source_refs:
  - "remote data-backup-before-compact-20261112 at d5815280bbb364046a0aeeae786655618307c2a0"
  - "remote compacted data tip 32488847ab94efc6ced07923c8762fa91648b25f"
  - "/tmp/kilo/compact-funnypot-data-history.py"
  - "/tmp/kilo/publish-funnypot-compacted-history.py"
  - "docker compose ps: healthy after restart"
tags:
  - "publication"
  - "history"
  - "deployment"
---

# Data branch compacted from 16200 updates into 162 checkpoints

Completed user-requested retrospective data history compaction on 2026-11-12. User also reported router port forwarding enabled; external reachability was not independently tested. Paused the Docker service, fetched final linear data tip d5815280bbb364046a0aeeae786655618307c2a0 containing 16200 commits, preserved full original history remotely as data-backup-before-compact-20261112, and created 162 oldest-to-newest checkpoint commits (each 100 original updates). Each checkpoint preserves its batch endpoint tree and metadata; final compacted tip 32488847ab94efc6ced07923c8762fa91648b25f has identical tree 1ae27170771ba2ddb110cae90298d97730955619 and empty diff against old tip. Normal push from a blob-filtered clone tried fetching unnecessary historical blobs and timed out without updating remote. Published the 162 new commit objects in a 48154-byte pack via Git receive-pack protocol with explicit old-tip and backup checks, server atomic old-tip comparison and unpack/connectivity verification. Service resumed healthy, API version endpoint responds on host 11433, and new startup-recovery plus normal session publications descend from the compacted tip (164 commits at first verification, more arriving normally). Frontend main stayed ac7f1268 unchanged. Host frontend checkout was stale with an unrelated local ahead commit and was not reset. This operation compacted existing history only; no scheduled future auto-squash was added. Operational scripts and isolated filtered bare clone are under /tmp/kilo; remote backup is the durable recovery source. Large pre-existing temporary pack files in host frontend object storage were observed but not deleted.
