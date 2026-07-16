---
id: mp-artifact-llm-response-flow
kind: fact
title: LLM Response Flow
scope: project
status: approved-by-request
created_at: 2026-07-12
author: Forge
source_refs:
  - FunnyPot/Program.cs
  - FunnyPot/AppConfiguration.cs
  - FunnyPot.Tests/UnitTests.cs
  - config/app-config.yaml
---

# LLM Response Flow

FunnyPot does not send every command to the LLM. It short-circuits known, local, static, SCP, binary-cat, CPU-info, and cached responses before using OpenRouter.

## Prompt Construction

- Each shell session starts `messageHistory` with `BuildSystemPrompt(username)`.
- `BuildSystemPrompt` defines the Omega-Black persona, Debian 6 host fingerprint, command baseline, protected file behavior, terminal-only output contract, positive bias toward plausible Linux output, binary-file handling, and meta-question lock.
- Each command sent to the LLM is wrapped by `BuildCommandUserPrompt(command)`, which labels the command as `single` or `chained` and requires raw terminal stdout/stderr only.
- History is trimmed to `MaxLlmHistoryMessages` while retaining the system prompt.

## Response Selection Order

1. Reject invalid input before resolution.
2. Treat SCP and SFTP commands as local built-ins and return empty shell output.
3. Handle binary executable `cat` locally with binary-looking ELF output.
4. Handle known built-ins and local filesystem mutations locally.
5. Return local fallback for non-Linux network-device probes, SUID discovery, and some fallback cases.
6. Use static responses from `FunnyPot/data/ssh_responses.jsonl` when a simple command has a known entry.
7. Reuse `CommandResponseCache` entries loaded from prior telemetry where available.
8. Rate-limit LLM calls per session key.
9. Call OpenRouter and normalize terminal output.
10. Replace API, network, context-length, and implausible `command not found` failures with local fallback output when appropriate.

## OpenRouter Behavior

- The API URL is built from configured base URL and chat endpoint.
- API keys are read through `GetSecretOrEnvironment("OPENROUTER_API_KEY")`.
- The primary model comes from `LLM_MODEL` or config, and fallback models come from `LLM_FALLBACK_MODELS` or config.
- Runtime attempts at most two distinct models.
- Requests set `max_tokens`, low temperature, and reasoning disabled.
- HTTP failures become `[api error]` responses and exceptions become `[network error]` responses.
- Missing API keys return `[api error] OpenRouter API key not configured` without making a network call.

## Rate Limiting And Fallbacks

- `LlmRateLimiter` defaults to 20 requests per 60 seconds per key unless overridden by `LLM_RATE_LIMIT_MAX` and `LLM_RATE_LIMIT_WINDOW_SECONDS`.
- Rate limit failures return a terminal response explaining the wait time and set `rateLimited=true`.
- CPU-info generation is a special LLM-assisted path that asks for JSON CPU shape and falls back to deterministic CPU info if parsing fails.

## Verified Behaviors

- Tests cover command classification, prompt constraints, OpenRouter response parsing, API URL construction, model failure detection, cache-safe local fallbacks, CPU-info parsing, and per-session rate limiting.
