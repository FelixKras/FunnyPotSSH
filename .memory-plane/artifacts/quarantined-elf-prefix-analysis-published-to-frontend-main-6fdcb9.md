---
id: "mp-20261120-324a6fdcb9"
kind: "episode"
title: "Quarantined ELF prefix analysis published to frontend main"
scope: "project"
status: "approved"
created_at: "2026-11-20T12:29:06.410415Z"
author: "Forge"
confidence: 1.0
approved_at: "2026-11-20T12:29:06.417830Z"
reviewer: "Forge"
source_refs:
  - "frontend-main commit 3cf83a5a; private quarantine sidecars and read-only byte analysis"
tags:
  - "malware"
  - "frontend"
---

# Quarantined ELF prefix analysis published to frontend main

Two Nov 19 SSH exec URL-download captures retained matching 5 MiB prefixes (13.88% of reported size). Recomputed SHA-256 matched both sidecars. x86-64 ELF, UPX markers and near-maximal entropy suggest packing; entry point outside retained data, no family or behavior confirmed. Public report excludes raw samples and private provenance. Only frontend commit 3cf83a5a pushed; frontend tests passed.
