---
id: "mp-20261126-e5aa468da0"
kind: "episode"
title: "Per-tab clean URL routing and interactive Attack Mind Map pilot tab"
scope: "project"
status: "approved"
created_at: "2026-11-26T16:28:05.134458Z"
author: "Antigravity"
confidence: 1.0
approved_at: "2026-11-26T16:28:05.142574Z"
reviewer: "Antigravity"
source_refs:
  - "frontend-main/index.html; frontend-main/404.html; scripts/test-ollama-frontend.mjs"
tags:
  - "frontend"
  - "ux"
  - "routing"
---

# Per-tab clean URL routing and interactive Attack Mind Map pilot tab

Implemented clean per-tab GitHub Pages URLs (/Overview, /Credentials, /Geography, /AttackerExchange, /CaughtBinaries, /Ollama, /AttackMindMap) using semantic tab links, History API routing with popstate/hashchange fallback, normalized siteBasePath asset URLs, and frontend-main/404.html SPA redirect handoff. Added the pilot Attack Mind Map tab (data-tab=mindmap) rendering each attack as a foldable, color-coded tree (Origin & Access, Command Kill-Chain, Caught Binaries & Malware, Tactics & Behavioral Signals) where every artefact node is clickable for plain-English explanations and raw evidence in a sticky inspector. Verified with extended scripts/test-ollama-frontend.mjs. Pending commit/push to frontend-main.
