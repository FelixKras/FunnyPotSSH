---
id: "mp-20261112-49ed1faa16"
kind: "decision"
title: "Ollama honeypot dedicated data-branch publication and dashboard tab"
scope: "project"
status: "approved"
created_at: "2026-11-12T04:55:46.739266Z"
author: "Forge"
confidence: 1.0
approved_at: "2026-11-12T04:55:46.747413Z"
reviewer: "Forge"
supersedes: "mp-20261112-0223bbc08f"
source_refs:
  - "FunnyPot/OllamaPublication.cs"
  - "FunnyPot/Program.cs"
  - "frontend-main/index.html"
  - "FunnyPot.Tests/OllamaPublicationTests.cs"
  - "scripts/test-ollama-frontend.mjs"
tags:
  - "ollama"
  - "publication"
  - "frontend"
---

# Ollama honeypot dedicated data-branch publication and dashboard tab

The zero-token unauthenticated Ollama/OpenAI TCP 11434 listener now publishes a separate data/ollama.json through Logger.PushToGit, in addition to private rotating JSONL captures. OllamaPublication exports latest 1000 connection records by ID, correlating peer/request/status/error and synthetic replies; public body previews capped at 4096 chars, Authorization never exported. Retained-window counters only, not lifetime totals. Snapshot restoration merges remote and local records by ID/latest UpdatedAt. Program publishes periodically using DATA_PUSH_INTERVAL_SECONDS even without SSH sessions. frontend-main/index.html adds independent Ollama / OpenAI tab with counters, search, outcome filters, pagination and escaped expandable request/reply details; missing optional dataset leaves SSH dashboard usable. Tests: all 152 .NET tests passed, node scripts/test-ollama-frontend.mjs passed syntax/escaping/filter/pagination/missing-data checks; Compose and whitespace validation passed. No code commit, manual data push, container redeploy or frontend deployment performed. Prior zero-token listener limits and private capture persistence remain in force; prior no-dashboard-publication decision is superseded by this requested bounded public dataset.
