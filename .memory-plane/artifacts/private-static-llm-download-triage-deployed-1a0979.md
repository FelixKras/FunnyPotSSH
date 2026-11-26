---
id: "mp-20261120-b04e1a0979"
kind: "episode"
title: "Private static LLM download triage deployed"
scope: "project"
status: "approved"
created_at: "2026-11-20T14:15:54.095059Z"
author: "Forge"
confidence: 1.0
approved_at: "2026-11-20T14:15:54.103926Z"
reviewer: "Forge"
source_refs:
  - "FunnyPot/MalwareToolReviewer.cs; Dockerfile; subscription-bridge tests; live Compose health and synthetic bridge check"
tags:
  - "malware"
  - "telemetry"
---

# Private static LLM download triage deployed

New quarantined URL downloads queue a bounded static review using file and readelf. The configured provider receives structured observations, not raw samples or command text. Drafts are separate private review sidecars, not public exports. Existing captures were not explicitly requeued. Container healthy, .NET and bridge suites passed, and a synthetic production bridge request returned valid JSON. JSON audit found duplicated timeline and structured exchange records, with credential fields in the frontend export; no schema migration performed.
