---
id: "mp-20261112-9c4e2a7f3b"
kind: "episode"
title: "Added Server header fingerprint, keyword + LLM Ollama responder, and local test filtering"
scope: "project"
status: "approved"
created_at: "2026-11-12T17:35:00Z"
updated_at: "2026-11-13T09:18:00Z"
author: "Forge"
confidence: 1.0
approved_at: "2026-11-13T09:18:00Z"
reviewer: "Forge"
source_refs:
  - "FunnyPot/OllamaHoneypot.cs"
  - "FunnyPot/OllamaPublication.cs"
  - "FunnyPot/Program.cs"
  - "FunnyPot.Tests/OllamaHoneypotTests.cs"
  - "FunnyPot.Tests/OllamaPublicationTests.cs"
tags:
  - "ollama"
  - "honeypot"
  - "fingerprinting"
  - "llm"
---

# Added Server header fingerprint, keyword + LLM Ollama responder, and local test filtering

1. **External Traffic Review**:
   - `199.165.159.217` / `199.165.159.129` (ShadowServer) and `123.58.200.147`: TCP probes with no HTTP request line.
   - `202.112.47.54` (`Mozilla/5.0 zgrab/0.x`) and `74.82.47.3` (Firefox): discovery requests (`GET /`, `/api/tags`, `/api/version`).
   - `91.92.33.192` (`ollama-scan/0.1.0`): discovery (`GET /api/version`, `/api/tags`, `/api/ps`) followed by `POST /api/pull` with `{"name":"richardyoung/gemma-4-12b-coder-abliterated:Q4_K_M"}`.
   - `45.112.149.170` (`Go-http-client/1.1`): repeated `GET /api/tags` polling and proxy verification `POST /v1/chat/completions` with prompt `"Compute 17 multiplied by 19. Reply with exactly ROUTER_OK 323 and nothing else."`.

2. **Protocol & Endpoint Additions (`OllamaHoneypot.cs`)**:
   - Added `Server: ollama/0.6.2` header to all HTTP responses and 429 rejections.
   - Added plausible responses for `POST /api/pull`, `/api/create`, `/api/copy`, `/api/delete` (NDJSON streaming manifest/digest/success or JSON status) and `POST /api/embed`, `/api/embeddings`, `/v1/embeddings`.
   - Implemented general keyword/pattern responder (`TryMatchPredefinedKeywordReply`): arithmetic solver (`multiplied by`, `*`, `+`, `-`, `/`), exact marker extractor (`Reply with exactly <TOKEN> ...`), `ping`/`pong`, `router_ok`/`status_ok`, system-prompt/jailbreak probes, model identity queries, and code/SQL/shell/JSON/translation/summary prompts.
   - Connected `Program.GetLLMResponseAsync` to `OllamaHoneypot` (`ResolveCompletionReplyAsync`) with a 2-slot concurrency semaphore and 8s timeout as fallback when no keyword rule matches, falling back to `OllamaHoneypot.Reply` on error/timeout.
   - Logged actual generated `response` in raw `ollama-connections.jsonl` and exported it via `OllamaPublication.Export`.

3. **Test Removal & Publication Filtering (`OllamaPublication.cs`)**:
   - Added `OllamaPublication.IsLocalOrPrivatePeer` to filter loopback (`127.0.0.0/8`, `::1`) and RFC1918 (`10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`) peers from published `data/ollama.json`.
   - Purged all 17 local test connection IDs (`127.0.0.1` and `172.18.0.1`) from container `/var/lib/funnypot/ollama/ollama-connections.jsonl` and `/home/test/app/frontend/data/ollama.json`, and pushed clean telemetry (`3a7c65210`) to the `data` branch.
   - All 190 .NET tests pass; container rebuilt, deployed, and verified healthy.

