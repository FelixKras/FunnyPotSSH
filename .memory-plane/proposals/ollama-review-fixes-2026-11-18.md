---
title: Local Ollama replies and public capture eligibility
scope: FunnyPot working tree
author: Codex
status: proposed
date: 2026-11-18
source_refs:
  - FunnyPot/OllamaHoneypot.cs
  - FunnyPot/OllamaPublication.cs
  - FunnyPot/Program.cs
  - FunnyPot.Tests/OllamaHoneypotTests.cs
  - FunnyPot.Tests/OllamaPublicationTests.cs
---

The listener resolves completions locally. Export and snapshot merge require a parseable eligible peer before retaining a row. The .NET suite passed all 190 tests and the Ollama frontend checks passed. Deployment was outside this change.
