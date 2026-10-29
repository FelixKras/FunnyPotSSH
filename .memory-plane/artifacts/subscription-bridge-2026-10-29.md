---
id: mp-artifact-subscription-bridge-2026-10-29
kind: fact
title: Single-container subscription bridge and host credential isolation
scope: project
status: approved-by-request
author: Forge
created_at: 2026-10-29
source_refs:
  - User request to replace full Kilo with a minimal Docker bridge and protect authentication
  - subscription-bridge/bridge.py
  - Dockerfile
  - subscription-bridge/supervise.py
  - subscription-bridge/healthcheck.py
  - subscription-bridge/test_supervise.py
  - subscription-bridge/test_bridge.py
  - subscription-bridge/test_exporter.py
  - scripts/export-subscription-token.py
  - config/systemd/funnypot-subscription-token.service
  - config/systemd/funnypot-subscription-token.timer
  - config/systemd/funnypot-subscription-stack.service
  - docker-compose.subscription.yaml
  - FunnyPot/SubscriptionBridgeAdapter.cs
  - FunnyPot.Tests/SubscriptionBridgeAdapterTests.cs
  - FunnyPot/Program.cs
  - docs/DEPLOY.md
  - https://github.com/Kilo-Org/kilo/blob/main/packages/opencode/src/plugin/codex.ts
  - Production direct bridge and .NET adapter checks, 2026-10-29
---

Implemented and deployed at the user's request, superseding the full Kilo server
used earlier that day. `LLM_PROVIDER=subscription-bridge` routes FunnyPot cache
misses over a Unix socket to a minimal Python-standard-library bridge **inside
the same FunnyPot container**, then directly to the fixed ChatGPT Codex Responses endpoint.
Model is `openai/gpt-6.1-terra`. There are no agents, tools, shell execution,
session store, or public bridge port. OpenRouter and full Kilo remain explicit
alternatives, not automatic fallbacks.

The initial sidecar implementation was replaced after the user explicitly
clarified that the bridge must be in the same container. Only
`funnypot-container` remains; the sidecar container and obsolete socket volume
were removed. Both network-facing processes are nonroot with different UIDs;
a minimal root PID 1 supervisor handles credential isolation and lifecycle.

The bridge accepts only text role/content messages, forces empty tools and no
server-side storage, preserves conversation roles, parses completed SSE output,
rejects tools/refusals/incomplete responses, and reports usage. Requests,
responses, connection count, and concurrent upstream calls are bounded. Redirects
and proxy-environment routing are disabled for authenticated upstream traffic.
Known Content-Length is required; .NET uses StringContent rather than chunked
JsonContent. Failure responses and diagnostics contain no upstream bodies,
headers, or credential values.

## Credential boundaries

The refresh token and other provider credentials stay in the host user's existing
Kilo auth store. Only the OpenAI access token, expiry, and account ID are exported
to a mode-0600 file inside a mode-0700 directory under
`/run/user/1000/funnypot-subscription/access/` (host tmpfs). That directory mounts
read-only beneath a root-owned mode-0700 parent at `/run/host-subscription/`,
so the honeypot UID cannot traverse it. An initial test export on persistent storage was
deleted when the tmpfs design was installed. No token values were logged,
committed, placed in container environment variables, or baked into images.

The root supervisor copies only the access export into bridge-owned private
tmpfs storage every 30 seconds. The app is UID 1000 and the bridge is UID 20001
(shared GID 1000 for socket access). Credential directory/file modes are 0700/0600;
socket directory/socket modes are 0750/0660, with ownership UID 20001, GID 1000.
The app can connect but cannot unlink the socket or read either credential path.

Both network-facing processes have zero effective/permitted capabilities and
no-new-privileges. PID 1 alone retains CHOWN, DAC_OVERRIDE, SETUID, SETGID, and
KILL to initialize/copy credentials, drop child UIDs, and forward shutdown signals.
It reaps descendants and stops both children if either exits. Core dumps are
disabled. Root-owned health-check code avoids running the app-owned assembly
as root. The default non-bridge image command remains the unprivileged .NET app;
the optional Compose overlay enables the supervisor and required capabilities.

OpenAI's observed access-token lifetime was about eight days, not one hour.
Frequent export does not shorten that lifetime. Compromising the bridge can
expose its current access token; host root/Docker administrators can access it.
Tmpfs may be swapped according to host configuration. Same-container UID
separation is weaker than separate container boundaries: a root compromise can
read the access token. The refresh token remains outside Docker. These protections
do not make the access token nonextractable or scope its provider permissions.

## Host lifecycle and refresh

An enabled user timer `funnypot-subscription-token.timer` runs the host-only
exporter every minute. It refreshes OAuth credentials within 120 seconds of
expiry, retains rotated refresh tokens in the canonical host auth file, preserves
other providers, and atomically exports access-only credentials. It serializes
exporter instances and detects auth-file changes during refresh, but Kilo does
not cooperate with that lock. Concurrent refresh with Kilo remains a residual
race; a separately authorized login is preferable for strict independence.

The enabled `funnypot-subscription-stack.service` invokes credential export before
starting Compose to recreate tmpfs credentials after reboot. User lingering is
already enabled. The ignored host-local Compose override now selects the bridge
and extends the tracked optional single-container Compose definition. Normal Compose
commands preserve this selection. `funnypot-kilo.service` is disabled/stopped;
the Telegram gateway and other Kilo usage are unaffected. Previous Kilo image is
tagged `funnypot:pre-minimal-bridge-20261029` for rollback. Data mounts were retained.
The previous two-container app image is also tagged
`funnypot:pre-single-container-20261029`.

## Verification

- 123 .NET tests and 17 Python tests passed, including role translation, no-tools
  enforcement, SSE failure rejection, redirect blocking, sanitized auth errors,
  token export permissions/field filtering, and mocked refresh rotation.
- Direct bridge request returned `Linux` using Terra with 38 input / 5 output
  tokens. Production `dotnet FunnyPot.dll --subscription-bridge-check` exercised
  the actual .NET Unix-socket transport and also returned `Linux`.
- A final uncached synthetic production SSH command printed the expected marker
  and `Linux` with the full Kilo server stopped. Tests used synthetic credentials
  accepted by the harvest policy, not the configured SSH secret.
  Telemetry confirmed `ResponseSource=llm`, `LlmModel=openai/gpt-6.1-terra`,
  3,639 prompt tokens, 21 completion tokens, and 1,860 ms response duration.
- After the same-container correction, only `funnypot-container` remains and
  becomes healthy. The actual app-user .NET bridge check returned `Linux`.
  Runtime audit verified app UID 1000 / bridge UID 20001, zero effective and
  permitted capabilities for both, and no-new-privileges. Attempts to open both
  credential paths as the app user raised PermissionError. The old full-Kilo
  TCP listener and sidecar are absent. Host and container credential copies use
  tmpfs, with access-only fields and mode 0600.
- The real refresh branch was not forced against the user's live refresh token;
  it was tested with mocked OAuth responses. Live access-token export and the
  periodic host exporter completed successfully.
