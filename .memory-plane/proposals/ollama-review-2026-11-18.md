---
title: Ollama listener review findings
scope: Ollama listener and publication in the local working tree
author: Codex
status: proposed
date: 2026-11-18
source_refs:
  - FunnyPot/Program.cs
  - FunnyPot/OllamaHoneypot.cs
  - FunnyPot/OllamaPublication.cs
  - docs/DEPLOY.md
---

Startup supplies the shared LLM responder despite the documented zero-upstream contract. Export can retain a request whose peer record was lost to rotation or pruning. Confirm the responder policy and require a known public peer before publication.
