---
id: "mp-20261112-05c7ce87f8"
kind: "episode"
title: "Ollama honeypot deployed healthy on host TCP 11433"
scope: "project"
status: "approved"
created_at: "2026-11-12T05:02:07.985445Z"
author: "Forge"
confidence: 1.0
approved_at: "2026-11-12T05:02:07.993282Z"
reviewer: "Forge"
supersedes: "mp-20261112-969a08287c"
source_refs:
  - "docker-compose.yaml"
  - "docker compose ps (2026-11-12: healthy, 11433->11434)"
  - "https://raw.githubusercontent.com/FelixKras/FunnyPot.ai/data/data/ollama.json"
  - "frontend-main/index.html"
tags:
  - "deployment"
  - "ollama"
---

# Ollama honeypot deployed healthy on host TCP 11433

Deployment completed 2026-11-12 using docker compose up -d --build funnypot after recording the host-port decision. Running funnypot-container is healthy with host TCP 11433 mapped to container TCP 11434; SSH remains 22722. Router external 11434 should forward to host 11433. Verified live /api/tags, OpenAI /v1/chat/completions and streaming Ollama /api/chat, all brief locally simulated replies with no upstream calls. Public raw GitHub data/data/ollama.json returned schema version 1 and captured verification requests, proving automatic data-branch publication in the supervised production environment. Tests passed: 152 .NET, 19 Python bridge/supervisor tests, frontend VM checks and Compose validation. Unrelated dirty changes preserved; no code commits or manual repository pushes performed. frontend-main/index.html has the new Ollama/OpenAI tab locally but frontend publication is still pending; backend image does not deploy the GitHub-hosted UI. This supersedes prior pending-backend-deployment status only; frontend is not claimed deployed.
