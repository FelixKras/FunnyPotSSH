---
id: mp-artifact-json-command-response-store
kind: decision
title: JSON Command Response Store
scope: project
status: approved-by-request
created_at: 2026-08-06
author: OpenCode
supersedes:
  - mp-artifact-cached-response-quality-session-2026-07-16
source_refs:
  - FunnyPot/CommandResponseStore.cs
  - FunnyPot/Program.cs
  - FunnyPot/data/command_responses.json
  - FunnyPot.Tests/UnitTests.cs
  - FunnyPot/AppConfiguration.cs
  - config/app-config.yaml
  - Dockerfile
  - docker-compose.yaml
---

# JSON Command Response Store

The project owner established a cache-first/LLM-only response architecture to prevent further growth of inadequate rule-based shell responses.

## Decision

- FunnyPot has one exact-command response dictionary: `command_responses.json`.
- The dictionary is loaded synchronously before the SSH listener starts.
- Keys use ordinal, exact matching. Case and internal or trailing whitespace are significant.
- Empty strings are valid cached responses.
- A cache miss is sent to OpenRouter. Successful model output is written atomically to the JSON dictionary and is available as a cache hit afterward.
- API and network failures are returned but not learned.
- Compound-command repair remains an LLM operation; only structurally complete output is learned.
- Input rejection and SCP/SFTP protocol control remain explicit non-shell-answer exceptions.
- Redis, SQL, NoSQL, telemetry reconstruction, hardcoded built-in answers, frequent-command rules, local response fallbacks, and LLM rate-limit fallback responses are not part of response selection.

## Persistence

- Docker uses the named volume `command-responses` mounted at `/var/lib/funnypot`.
- `COMMAND_RESPONSE_PATH` points to `/var/lib/funnypot/command_responses.json` in Compose.
- The image seeds a new volume from the reviewed JSON dictionary; learned entries survive container recreation.
- Runtime updates write a same-directory temporary file, flush it, then atomically replace the active JSON file.

## Migration

- The former `ssh_responses.jsonl` and code-generated dynamic responses were retired.
- Reviewed, non-dynamic JSONL entries were migrated into 135 exact seed entries.
- The reviewed `chattr`/`lockr` command variants were retained as JSON seed entries rather than code rules.

## Verification

- Tests cover exact matching, empty responses, durable learning, concurrent updates, cache-hit LLM bypass, miss learning, failure non-learning, compound repair, SCP protocol handling, and seed-file validity.
- A live uncached `uname -r` SSH command was answered by `openai/gpt-5.6-luna`, persisted with `origin: llm`, served unchanged on the next request, and remained present after forced container recreation.
