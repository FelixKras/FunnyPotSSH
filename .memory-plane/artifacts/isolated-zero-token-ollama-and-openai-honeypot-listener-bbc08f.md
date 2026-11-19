---
id: "mp-20261112-0223bbc08f"
kind: "decision"
title: "Isolated zero-token Ollama and OpenAI honeypot listener"
scope: "project"
status: "approved"
created_at: "2026-11-12T04:51:16.515061Z"
author: "Forge"
confidence: 1.0
approved_at: "2026-11-12T04:51:16.524015Z"
reviewer: "Forge"
source_refs:
  - "FunnyPot/OllamaHoneypot.cs"
  - "FunnyPot.Tests/OllamaHoneypotTests.cs"
  - "docker-compose.yaml"
  - "docs/DEPLOY.md"
tags:
  - "ollama"
  - "honeypot"
---

# Isolated zero-token Ollama and OpenAI honeypot listener

Added FunnyPot/OllamaHoneypot.cs, started by Program, with unauthenticated TCP 11434 and local canned completions only; never invokes real LLM adapters. Ollama discovery/chat/generate and OpenAI models/chat/completions support JSON, NDJSON or SSE. Defaults: 12 connections per IP per fixed minute, 120 globally, 16 active handlers, 10 second deadlines, 8 KiB headers, 16 KiB bodies. TCP connections, request bodies and rejects are captured separately with IDs; Authorization omitted. Compose persists capture under FunnyPot/data/ollama/ollama-connections.jsonl, excluded from Git and Docker build context; ten 10 MiB archives, no dashboard publication. Tests: dotnet test FunnyPot.sln --no-restore passed 149 tests; docker compose config --quiet and git diff --check passed. Code/config implementation only; running production container was not rebuilt or restarted in this task. Existing unrelated dirty work preserved.
