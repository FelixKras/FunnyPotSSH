---
id: mp-artifact-project-overview
kind: fact
title: FunnyPot Project Overview
scope: project
status: approved-by-request
created_at: 2026-07-12
author: Forge
source_refs:
  - FunnyPot/Program.cs
  - FunnyPot/AppConfiguration.cs
  - FunnyPot/FakeFileSystem.cs
  - FunnyPot/TelemetryWriteQueue.cs
---

# FunnyPot Project Overview

FunnyPot is an SSH honeypot and attacker-interaction research project. Its code and documentation center on SSH sessions, command handling, LLM-backed responses, fake filesystem behavior, telemetry logging, static command responses, SCP upload capture, and research/data harvesting workflows.

Core implementation areas identified by the current projection include:

- SSH session orchestration and command response flow in `FunnyPot/Program.cs`.
- Configuration loading through `FunnyPot/AppConfiguration.cs`.
- Fake filesystem state and path handling in `FunnyPot/FakeFileSystem.cs`.
- Asynchronous telemetry/log writing in `FunnyPot/TelemetryWriteQueue.cs`.
- AutoResearch workflow in `FunnyPot/AutoResearchRunner.cs` and `autoresearch/`.
- Unit coverage in `FunnyPot.Tests/UnitTests.cs`.

This overview is intentionally compact. Detailed claims should be captured as separate artifacts or proposals with direct source references.
