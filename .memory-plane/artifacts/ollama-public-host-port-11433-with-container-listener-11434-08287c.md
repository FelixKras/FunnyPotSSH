---
id: "mp-20261112-969a08287c"
kind: "decision"
title: "Ollama public host port 11433 with container listener 11434"
scope: "project"
status: "approved"
created_at: "2026-11-12T04:58:07.478777Z"
author: "Forge"
confidence: 1.0
approved_at: "2026-11-12T04:58:07.487546Z"
reviewer: "Forge"
source_refs:
  - "docker-compose.yaml"
  - "docs/DEPLOY.md"
tags:
  - "ollama"
  - "deployment"
---

# Ollama public host port 11433 with container listener 11434

User requested deployment and host TCP port 11433 on 2026-11-12. Compose now maps OLLAMA_HONEYPOT_HOST_PORT default 11433 to OLLAMA_HONEYPOT_PORT default 11434 inside the container. Router external TCP 11434 should target host TCP 11433. Updated docs/DEPLOY.md examples and overrides. All previously implemented local simulation, throttling, separate data/ollama.json publication and frontend tab remain. Deployment is pending verification; use direct Compose rather than auto-commit/push deploy helper to preserve unrelated dirty work.
