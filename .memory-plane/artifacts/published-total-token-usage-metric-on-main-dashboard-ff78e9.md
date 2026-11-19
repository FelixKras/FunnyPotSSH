---
id: "mp-20261112-bc88ff78e9"
kind: "episode"
title: "Published total-token usage metric on main dashboard"
scope: "project"
status: "approved"
created_at: "2026-11-12T06:10:55.857550Z"
author: "Forge"
confidence: 1.0
approved_at: "2026-11-12T06:10:55.866895Z"
reviewer: "Forge"
source_refs:
  - "frontend-main commit 9bcd97cc"
  - "scripts/test-ollama-frontend.mjs"
  - "https://felixkras.github.io/FunnyPot.ai/"
tags:
  - "frontend"
  - "tokens"
---

# Published total-token usage metric on main dashboard

Added Total Tokens Used card to main Honeypot Performance section and published frontend main commit 9bcd97cc. Sums TotalPromptTokens and TotalCompletionTokens from global_stats.json with formatted prompt/completion breakdown. Excludes synthetic Ollama/OpenAI API token fields because no upstream calls occur. Described as recorded SSH usage totals, not provider billing or guaranteed lifetime accounting. Missing/invalid/negative counters show unavailable rather than false zero. Latest published counters observed during implementation: 6088755 prompt and 67215 completion, totaling 6155970. Node frontend tests passed for totals, breakdown, zero, missing and invalid counters plus all existing API UI tests. Verified live Pages contains totalTokensUsed card. Only frontend index committed; parent test update remains local, unrelated changes preserved. No backend restart or data publication schedule change.
