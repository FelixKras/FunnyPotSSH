---
id: "mp-20261112-a770084bfc"
kind: "episode"
title: "Published SSH-style API exchange cards and Overview API statistics"
scope: "project"
status: "approved"
created_at: "2026-11-12T06:07:59.195340Z"
author: "Forge"
confidence: 1.0
approved_at: "2026-11-12T06:07:59.203447Z"
reviewer: "Forge"
source_refs:
  - "frontend-main commit 7e4538fc"
  - "scripts/test-ollama-frontend.mjs"
  - "https://felixkras.github.io/FunnyPot.ai/"
  - "docs/DEPLOY.md"
tags:
  - "frontend"
  - "ollama"
  - "deployment"
---

# Published SSH-style API exchange cards and Overview API statistics

Implemented and published frontend main commit 7e4538fc on 2026-11-12, changing only frontend-main/index.html in the frontend repository. Ollama/OpenAI tab now uses paired attacker-input and honeypot-response inspector cards matching SSH exchange styling, with per-role JSON conversation messages, prompt/system inputs, requested model, streaming flag, HTTP outcome, local simulation source and expandable raw evidence. Missing-body explanations distinguish GET discovery, pre-read throttling, errors and pending TCP connections. Partial/truncated or non-JSON captures remain visible as raw text. Overview now has separate API retained-window metrics for connections, source IPs, successful requests, completion replies, throttles, errors, pending outcomes and zero upstream tokens; never mixed into SSH totals. Missing optional API dataset shows unavailable counts rather than zero traffic. Reuses responsive inspector grid; all attacker-controlled text escaped and multimodal URLs are never loaded as resources. Node tests passed for role extraction, overview metrics, truncation, missing-body states, XSS escaping, pagination and search. Verified live GitHub Pages HTTP 200 includes new Overview elements and paired card renderer. Updated parent docs/tests locally; did not commit unrelated parent changes. Hourly data publication and running backend unchanged.
