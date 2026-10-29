---
id: mp-artifact-kilo-subscription-adapter-2026-10-29
kind: fact
title: Optional Kilo subscription-backed terminal adapter
scope: project
status: approved-by-request
author: Forge
created_at: 2026-10-29
source_refs:
  - FunnyPot/KiloLlmAdapter.cs
  - FunnyPot/Program.cs
  - FunnyPot/AppConfiguration.cs
  - FunnyPot.Tests/KiloLlmAdapterTests.cs
  - config/kilo/kilo.json
  - config/app-config.yaml
  - docker-compose.yaml
  - docs/DEPLOY.md
  - Local Kilo OpenAPI inspection and live Terra smoke test, 2026-10-29
  - dotnet test FunnyPot.sln --no-restore, 2026-10-29 (119 passed)
  - git diff --check and docker compose config --quiet, 2026-10-29 (passed)
---

Implemented, deployed, and recorded at the user's request. `LLM_PROVIDER=kilo` selects
the Kilo HTTP session adapter, while the default remains OpenRouter. The Kilo
model setting is separate (`KILO_MODEL`, default `openai/gpt-6.1-terra`). No
automatic fallback to OpenRouter occurs in Kilo mode, and its startup check
is skipped. Existing response caching and terminal normalization remain in
place.

Each request creates a fresh session with wildcard-deny permissions, uses the
dedicated text-only agent and disabled tools, supplies FunnyPot history as a
JSON transcript, and aborts/deletes the session in cleanup. Timeouts include
queueing, concurrency is bounded, and failed/incomplete/tool responses become
non-learnable API errors. Cleanup failures are logged. Token telemetry includes
cached input and reasoning output where reported by Kilo.

Deployment requires a dedicated Kilo server authenticated by the host user,
an isolated directory with the supplied agent config, and restricted network
access. Kilo's provider credentials are not mounted into the honeypot; a separate
local server password is mounted as a Docker secret. Setup and Docker host-network
reachability requirements are recorded in docs/DEPLOY.md.

Local Kilo OpenAPI confirmed the session, prompt, abort, deletion, permission,
and token schemas. Provider listing exposed `openai/gpt-6.1-terra`; a live
text-only request returned `Linux`, finish `stop`, without tool parts. Its
temporary session was aborted and deleted. No credentials were recorded.
Both temporary verification servers were stopped after testing. Full suite:
119 passed, zero failed. Patch whitespace and Compose validation passed.

## Production deployment (subsequent user request, 2026-10-29)

Production was deployed using direct `docker compose build` and
`docker compose up -d --no-build` commands, avoiding the canonical script's
automatic Git commits/pushes. The previously running image was preserved as
`funnypot:pre-kilo-20261029` and the command dictionary was archived under
ignored `backups/` before replacement. Cache bind mount and publication volume
were retained. The new image ID is
`sha256:b5d40386f3cac2089fd817fbd3e956ff512dde113d3051105efdb447d01be0eb`.

An enabled, active user systemd service `funnypot-kilo.service` runs the dedicated
Kilo server with `--pure`, binding only the existing Docker bridge gateway
`172.18.0.1:4197`. User lingering was already enabled. Its isolated config and
protected password reside in `/home/felix/.local/share/funnypot-kilo/`; no secret
values are recorded here. The ignored local `docker-compose.override.yaml`
selects Kilo/Terra and mounts the server password at
`/run/secrets/kilo_server_password`. The adapter now reads that password with
the existing secret-or-environment helper. Default tracked provider settings
remain OpenRouter for other deployments.

Verification: 119 unit tests passed, Compose and whitespace validation passed,
the recreated container became healthy, and the explicit application readiness
probe passed. An uncached synthetic SSH command printed the expected deployment
marker followed by `Linux`; runtime `command_result` telemetry reported
`ResponseSource=llm`, `LlmModel=openai/gpt-6.1-terra`, 5,139 prompt tokens,
54 completion tokens, and 9,049 ms response duration. Synthetic login probes
used nonsecret test credentials and the honeypot's third-attempt harvest policy;
they did not use or publish the configured SSH password. The existing one-session
limit required waiting for failed authentication connections to be released.

Host paths, restart behavior, and rollback commands are recorded in
docs/DEPLOY.md. The user subsequently requested Git commit and push of the
implementation and durable memory. Host-local configuration, credentials, and
the runtime-generated smoke-test cache entry are excluded from that commit.
