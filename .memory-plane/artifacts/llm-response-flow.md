---
id: mp-artifact-llm-response-flow
kind: fact
title: LLM Response Flow
scope: project
status: approved-by-request
created_at: 2026-07-12
updated_at: 2026-08-06
author: Forge
source_refs:
  - FunnyPot/Program.cs
  - FunnyPot/AppConfiguration.cs
  - FunnyPot.Tests/UnitTests.cs
  - config/app-config.yaml
  - docker-compose.yaml
---

# LLM Response Flow

FunnyPot uses one exact-command JSON dictionary before OpenRouter. This artifact's former layered static, telemetry-cache, built-in, and fallback flow was superseded by `mp-artifact-json-command-response-store` on 2026-08-06.

## Prompt Construction

- Each shell session starts `messageHistory` with `BuildSystemPrompt(username)`.
- `BuildSystemPrompt` defines the Omega-Black persona, Debian 6 host fingerprint, command baseline, protected file behavior, terminal-only output contract, positive bias toward plausible Linux output, binary-file handling, and meta-question lock.
- Each command sent to the LLM is wrapped by `BuildCommandUserPrompt(command)`, which labels the command as `single` or `chained` and requires raw terminal stdout/stderr only.
- History is trimmed to `MaxLlmHistoryMessages` while retaining the system prompt.
- Compound commands receive additional structured guidance: fixed synthetic host facts, extracted visible `echo`/`printf` label prefixes, an execution checklist, and a worked assignment/substitution example suitable for weaker models.
- Exact cache hits are returned without rule-based reinterpretation.
- If an LLM response omits an extracted visible label, the resolver retries once with a repair prompt containing the command, previous response, and required labels. Only a structurally complete repair replaces the first response.

## Response Selection Order

1. Reject invalid input before resolution.
2. Handle SCP and SFTP protocol control outside shell response selection.
3. Return an ordinal exact hit from `command_responses.json`, including valid empty responses.
4. Send every miss to OpenRouter and normalize terminal output.
5. Repair incomplete compound model output through OpenRouter when required.
6. Persist successful, structurally complete model output atomically into the same JSON dictionary.

## OpenRouter Behavior

- The API URL is built from configured base URL and chat endpoint.
- API keys are read through `GetSecretOrEnvironment("OPENROUTER_API_KEY")`.
- The default primary model is OpenRouter's `openai/gpt-5.6-luna`.
- The primary model comes from `LLM_MODEL` or config, and fallback models come from `LLM_FALLBACK_MODELS` or config.
- Runtime attempts at most two distinct models.
- Requests set `max_tokens`, low temperature, and reasoning disabled.
- HTTP failures become `[api error]` responses and exceptions become `[network error]` responses.
- Missing API keys return `[api error] OpenRouter API key not configured` without making a network call.

## Failure Behavior

- API and network failures are returned directly and are not written to the response dictionary.
- No rule-based shell fallback replaces model output.

## Verified Behaviors

- Tests cover exact JSON hits, LLM misses, durable learning, compound-label repair, OpenRouter parsing, API URL construction, and model-failure non-learning.
