---
id: mp-artifact-configuration-secrets
kind: procedure
title: Configuration And Secrets Rules
scope: project
status: approved-by-request
created_at: 2026-07-12
author: Forge
source_refs:
  - FunnyPot/AppConfiguration.cs
  - FunnyPot/Program.cs
  - config/app-config.yaml
  - docker-compose.yaml
  - docs/docker-secrets.md
  - run-docker.sh
---

# Configuration And Secrets Rules

FunnyPot uses YAML defaults, `.env` loading, environment overrides, and optional Docker secret files. Memory Plane artifacts must not store real credential values.

## Config Loading

- `AppConfiguration.Load` defaults to `config/app-config.yaml` relative to the app directory, then falls back to the working directory config path.
- Missing or invalid config files fall back to default `AppConfiguration` values rather than crashing.
- YAML uses hyphenated property names.
- `Program.LoadDotEnvFiles` loads `.env` from the app directory and current working directory before runtime settings are read.

## Runtime Overrides

- `LoadRuntimeSettings` applies environment overrides for auth tries, password harvest attempt, LLM delay, max sessions, idle timeout, SSH banner, SSH port, log directory, banner pool, and host key.
- Docker Compose passes common runtime variables including SSH credentials, SSH port, auth settings, LLM model and delay, session limits, data push interval, OpenRouter key, GitHub publication settings, LLM rate limits, banner list, and banner rotation interval.
- `LLM_MODEL` and `LLM_FALLBACK_MODELS` override configured model choices.
- `GITHUB_DATA_BRANCH` defaults to `data` at publication time.

## Secret Lookup

- `GetSecretOrEnvironment(name)` first checks `/run/secrets/<lowercase-name>`.
- If no non-empty Docker secret file exists, it falls back to the environment variable with the original name.
- This lookup is used for `OPENROUTER_API_KEY`, `GITHUB_TOKEN`, `GITHUB_USER`, static-site remote URL settings, and repository settings.

## Deployment Secret Rules

- The Compose deployment reads sensitive values from local `.env` and passes them into the container environment.
- The app can also read Docker secrets mounted by another deployment system.
- `.env` is expected to be ignored by Git and excluded from the Docker build context.
- Static dashboard publication needs `GITHUB_TOKEN` and `GITHUB_USER`; remote target can be set through `GITHUB_REPOSITORY`, `STATIC_SITE_REMOTE_URL`, `GITHUB_REMOTE_URL`, or `GITHUB_REPO` plus `GITHUB_USER`.

## Required Safety Rule

- Do not store real OpenRouter keys, GitHub tokens, SSH passwords, private keys, or raw personal data in `.memory-plane/`.
- It is acceptable to record variable names, config paths, and secret-loading behavior without secret values.
