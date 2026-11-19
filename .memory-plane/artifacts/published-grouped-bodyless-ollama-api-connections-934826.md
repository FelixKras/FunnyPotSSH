---
id: "mp-20261112-7651934826"
kind: "episode"
title: "Published grouped bodyless Ollama API connections"
scope: "project"
status: "approved"
created_at: "2026-11-12T06:34:52.383225Z"
author: "Forge"
confidence: 1.0
approved_at: "2026-11-12T06:34:52.446819Z"
reviewer: "Forge"
source_refs:
  - "frontend-main commit c6e7cd1f"
  - "scripts/test-ollama-frontend.mjs"
  - "https://felixkras.github.io/FunnyPot.ai/"
tags:
  - "frontend"
  - "ollama"
---

# Published grouped bodyless Ollama API connections

Frontend main commit c6e7cd1f groups all filtered Ollama/OpenAI connections with null, empty or whitespace-only request bodies into one summary panel instead of individual exchange cards. Group shows total connections, unique source IPs, outcome counts, time range and collapsed endpoint-count breakdown. Search/outcome filters apply before grouping. Pagination counts only body-bearing exchanges, preserving full raw capture and retained-window metrics unchanged. Bodyless-only filtered results show group rather than false empty state; prompts, partial bodies and JSON body {} remain individual. Node frontend tests passed for mixed 60-bodyless/29-bodied fixture, count correctness, body-only pagination, bodyless-only search, XSS escaping and previous token/statistics/exchange checks. Normal push to origin/main completed and live Pages HTTP 200 verified new renderer. No backend restart, capture deletion or data schedule changes; parent test edits remain local.
