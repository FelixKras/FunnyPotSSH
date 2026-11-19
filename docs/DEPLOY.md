# Deployment

FunnyPot runs in Docker. The host sees a `dotnet FunnyPot.dll` process
in `ps -ef`, but that is misleading — it is the container's PID 1
surfaced through `/proc`. Always check `docker ps -a`, not the host
process table, to find the running instance.

## Topology

| Layer | Detail |
| --- | --- |
| Container | `funnypot-container` (one replica) |
| Image | `funnypot-funnypot:latest`, built from `Dockerfile` |
| Compose | `docker-compose.yaml` at repo root |
| Exposed port | `22722` (SSH honeypot, configurable via `SSH_PORT` env) |
| Healthcheck | `bash -c '</dev/tcp/localhost/22722'` every 30s |
| Restart policy | `unless-stopped` |
| User inside container | `test` (uid 1655), no new privileges, all caps dropped |
| Log dir inside container | `/var/log/funnypot/` |
| App dir inside container | `/home/test/app/` |
| Response store | `/var/lib/funnypot/command_responses.json` on the `command-responses` named volume |

## Simulated Ollama / OpenAI honeypot

The same application listens on container TCP **11434**, published by Compose
on host TCP **11433**. Forward router TCP port 11434 to this host's TCP port 11433.
No password or API key is required; Authorization headers are ignored and are
not recorded. This is a local simulation, **not** a proxy to the real LLM or
subscription bridge: prompts cannot spend tokens or select an upstream provider.
Completion replies use local keyword rules and a canned fallback.

Supported discovery: `GET /`, `/api/version`, `/api/tags`, `/api/ps`, `/v1/models`,
and `POST /api/show`. Supported completions: `POST /api/chat`, `/api/generate`,
`/v1/chat/completions`, `/v1/completions`. Ollama supports JSON and NDJSON
(streaming defaults on); OpenAI supports JSON and SSE (`stream: true`). This is
a minimal compatibility surface, not a full Ollama implementation. Model pulls,
embeddings and tools are not implemented. Both Content-Length and HTTP/1.1
`Transfer-Encoding: chunked` request bodies are supported; chunked prompts are
decoded before JSON parsing and capture. Other transfer codings and ambiguous
Content-Length plus Transfer-Encoding requests are rejected.

Abuse limits (charged per TCP connection, including discovery and incomplete
requests): **12 per source IP per minute**, **120 globally per minute**,
**16 concurrent requests**, **10-second total connection timeout**, **8 KiB
headers**, and **16 KiB request bodies**. Limits use a fixed one-minute window.
For chunked requests the body limit applies to decoded bytes, with at most 1,024
nonempty chunks, 1 KiB metadata lines, and 8 KiB total chunk framing/trailers.
Chunk extensions are ignored and trailer values are not recorded. Partial bodies
received before disconnect, timeout, or malformed framing are retained in capture.
Excess connections receive a best-effort HTTP 429 and are closed immediately;
every request uses `Connection: close`. Source IP comes from the actual TCP peer,
never spoofable forwarding headers.

Each accepted TCP connection, request body/prompt, status, timeout/error, and
throttle rejection is captured independently from SSH logs in JSONL. Connection
and request records share an `id`. In Compose the file persists on the host at
`FunnyPot/data/ollama/ollama-connections.jsonl` (container:
`/var/lib/funnypot/ollama/ollama-connections.jsonl`). Files rotate at 10 MiB with
ten archives; older captures expire. Raw captures are ignored by Git and Docker
build context. Back them up separately if longer retention is required.

A separate **`data/ollama.json`** is generated in the publication repository and
staged/pushed to the configured `data` branch by `Logger.PushToGit`. It retains
the latest **1,000 connections**, correlating TCP peers with requests, status,
user agent, errors, and locally simulated completion replies. Public request
bodies are capped at **4,096 characters** with a truncation flag. Authorization
headers are never included. Prompts and source endpoints in this dedicated
dataset are public just like the existing SSH telemetry.

The dashboard's **Ollama / OpenAI** tab loads this file separately from SSH data,
with retained-window counters, search, outcome filters, pagination, and paired
attacker-input / honeypot-response inspector cards styled like the SSH exchange.
JSON messages are shown by conversation role, with model/streaming metadata and
expandable raw evidence. Missing bodies distinguish discovery, throttle and
incomplete/error cases. Overview also displays separate API counters (connections,
source IPs, successful requests, completion replies, throttles, errors, pending
outcomes and zero upstream tokens); these are not added to SSH totals. A missing
file does not prevent the SSH dashboard loading and shows unavailable API counts
rather than falsely reporting zero traffic.
One shared publication timer runs every `DATA_PUSH_INTERVAL_SECONDS` (default
**3600 seconds / one hour**) for both SSH and API telemetry, even if the API
listener is disabled. Session/shell endings and incoming events never bypass this
schedule. Local capture continues immediately; dashboard freshness can lag by up
to an hour. Startup recovery can publish pending data once after a restart; routine
publication creates at most one commit per hour, and only when files changed.
Public export and snapshot restoration omit requests with missing or unparseable peers.
Snapshot restoration merges by connection ID and latest update rather
than overwriting remote records. Raw logs recover local records across restarts;
the publication file remains bounded and is not a lifetime archive.

Environment overrides:

- `OLLAMA_HONEYPOT_ENABLED=false` disables the listener.
- `OLLAMA_HONEYPOT_HOST_PORT` changes the published host port (default `11433`).
- `OLLAMA_HONEYPOT_PORT` changes the container listener port (default `11434`).
- `OLLAMA_HONEYPOT_PER_IP_PER_MINUTE` (default `12`).
- `OLLAMA_HONEYPOT_GLOBAL_PER_MINUTE` (default `120`).
- `OLLAMA_HONEYPOT_LOG_PATH` changes the capture file when running outside Compose;
  Compose explicitly selects the persistent path above.

Deploy without invoking auto-commit/push deployment helpers:

```bash
docker compose up -d --build funnypot
curl http://127.0.0.1:11433/api/tags
curl http://127.0.0.1:11433/v1/chat/completions \
  -H 'Content-Type: application/json' \
  -d '{"model":"llama3.2:latest","messages":[{"role":"user","content":"Hi"}],"stream":false}'
```

## Deploy paths

In order of preference:

1. **`scripts/deploy.sh`** (canonical) — validates clean git state in
   any submodules, then `docker compose up -d --build`. This is the
   path to use from CI or after a multi-repo change. Lives at
   `scripts/deploy.sh`.

2. **`run-docker.sh`** (manual) — `docker compose up -d --build`
   plus a bring-up SSH auth probe via `setsid ssh -p 22722 test@127.0.0.1`.
   Use this when iterating locally. Lives at `run-docker.sh`.

3. **Raw `docker compose up -d --build`** — works but skips the
   submodule check and the bring-up probe. Not recommended.

## Standard redeploy

```bash
git push origin main
scripts/deploy.sh           # or: run-docker.sh
docker ps --format "table {{.Names}}\t{{.Status}}"
```

## Verification

- `docker ps -a` should show `funnypot-container` `Up ... (healthy)`.
- `docker inspect --format='{{json .State.Health}}' funnypot-container`
  should report `"Status": "healthy"` after at most 3 healthcheck
  intervals (90s).
- `timeout 5 bash -c 'cat </dev/tcp/localhost/22722'` should print the
  SSH banner (`SSH-2.0-OmegaBlack_Classified_Server_v1.0` by default).

## Cold start

A freshly built image takes **2–3 minutes** before the SSH port
binds. The dotnet process starts immediately (PID 1, ~13 .NET
threads), but JIT compilation of `FunnyPot.dll`, command-response
dictionary loading, and LLM client setup run before the listener is up.
The healthcheck will report `starting` (exit 1, "Connection refused")
during this window. Do not assume the deploy is broken just because
the first two healthchecks fail.

To confirm the app is making progress (not hung) without waiting for
the healthcheck, inspect the listening sockets inside the container:

```bash
docker exec funnypot-container cat /proc/1/net/tcp
```

A row with `local_address` ending in `:58D2` (hex for 22722) in state
`0A` (TCP_LISTEN) means the SSH server is bound and the deploy is
working. Anything else and the app is still starting up.

## Minimal subscription bridge (current production)

Production now uses `LLM_PROVIDER=subscription-bridge`, replacing the full Kilo
server with a small Python-standard-library transport **inside the same
`funnypot-container`**, alongside the SSH application. There is no sidecar.
The Kilo provider below remains an optional fallback architecture,
not the active production path.

```text
One container: [ SSH app UID 1000 -- Unix socket --> bridge UID 20001 ] --> ChatGPT
                                                    ^
                          bridge-private credential copy (container tmpfs)
                                                    ^
                  root supervisor <-- protected read-only host access-token mount
                                                    ^
                         host exporter timer / existing Kilo OAuth login
```

The bridge fixes the upstream URL to
`https://chatgpt.com/backend-api/codex/responses` and the model to
`gpt-6.1-terra`. It translates text messages into Responses API input, disables
tools and storage, parses completed SSE responses, and returns text and usage.
It accepts no endpoint, headers, tools, or arbitrary provider options from the
honeypot. It does not run agents, execute commands, or maintain sessions.

### Credential protection

- The long-lived refresh token and other providers' credentials remain in the
  host user's existing Kilo auth store, never mounted into the container.
- A host-only exporter selects the OpenAI OAuth entry, refreshing it when
  necessary, and writes **only access token, expiry, and account ID** into a
  mode-0600 file in a mode-0700 directory under `/run/user/1000/` (tmpfs).
- The export directory is mounted read-only under `/run/host-subscription/`,
  whose parent is root-owned mode 0700. The honeypot UID cannot traverse it,
  even though the host-exported file is owned by UID 1000.
- A root-owned PID 1 supervisor copies only access/expiry/account ID into
  bridge-owned, mode-0700 tmpfs storage every 30 seconds. The credential file
  is mode 0600. The app cannot read either the source or private copy.
- The app runs as UID 1000; the bridge as UID 20001. Both have zero effective
  and permitted capabilities and no-new-privileges. Only the small supervisor
  retains CHOWN, DAC_OVERRIDE, SETUID, SETGID, and KILL for credential copying,
  UID separation, and signal forwarding. Core dumps are disabled.
- The socket directory is bridge-owned/group-readable, not app-writable; only
  the socket itself is group-writable for connection. No bridge TCP port exists.
  There are no secrets in images, environment variables, arguments, or logs.
- Health checks use a root-owned Python probe, **not an app-owned DLL executed
  as root**. Run manual .NET commands with `docker exec --user test`.
- HTTPS verification is enabled and redirects/proxy-environment routing are
  rejected for credential-bearing upstream requests.
- **Access tokens are not necessarily short-lived:** the actual issuer expiry
  observed during setup was about eight days. Exporting them every minute does
  not shorten or revoke them. A bridge compromise could steal the current access
  token until expiry/revocation; host/Docker administrators can also access it.
  Tmpfs can be swapped unless the host's swap configuration prevents this.

The single-container design uses Unix-user/file-permission isolation rather than
separate container boundaries. A container-root compromise can access the access
token. The refresh token remains outside the container even in that case.

The exporter updates the existing Kilo auth store after an OAuth refresh to
retain rotated refresh tokens. It locks competing exporter instances, checks
for concurrent changes, and preserves other providers. Kilo does not honor that
lock: simultaneous refreshes are a residual race, not a guaranteed-safe shared
refresh protocol. A separately authorized account/login is preferable for strict
credential independence. Refresh failure fails closed; it never falls back to
OpenRouter or exports a refresh token to Docker.

### Setup and lifecycle

For this host (UID/GID 1000; adjust Compose paths/users and units for another host):

```bash
python3 -B scripts/export-subscription-token.py \
  --output /run/user/1000/funnypot-subscription/access/access.json
cp config/systemd/funnypot-subscription-token.{service,timer} ~/.config/systemd/user/
systemctl --user daemon-reload
systemctl --user enable --now funnypot-subscription-token.timer
docker compose -f docker-compose.yaml -f docker-compose.subscription.yaml up -d --build --remove-orphans
```

The host-local ignored `docker-compose.override.yaml` selects this same topology,
so ordinary `docker compose` commands use the bridge in production. The
`funnypot-subscription-token.timer` exports every minute; its oneshot service is
normally `inactive (dead)` after a successful run. The enabled
`funnypot-subscription-stack.service` first invokes the exporter and then starts
the stack, recreating tmpfs credentials before container startup after reboot.
The supplied user units assume the repository is at `~/Opencode/FunnyPot`.
User lingering is enabled on this host. Production's former
`funnypot-kilo.service` is disabled/stopped; other Kilo services are unaffected.

Verification:

```bash
python3 -B -m unittest discover -s subscription-bridge
dotnet test FunnyPot.sln --no-restore
docker compose ps
docker exec funnypot-container python3 -I -B /opt/subscription-bridge/healthcheck.py
docker exec --user test funnypot-container dotnet FunnyPot.dll --subscription-bridge-check
systemctl --user list-timers funnypot-subscription-token.timer
```

The model check makes a real, uncached `uname -s` request through the actual
.NET Unix-socket client and returns a nonzero status unless it gets `Linux`.
The original Kilo-based application image is preserved as
`funnypot:pre-minimal-bridge-20261029`. Switching back requires restoring the
Kilo-specific local override and restarting the old host service; do not simply
run the old image against the new bridge socket configuration.
The immediately preceding two-container image is retained as
`funnypot:pre-single-container-20261029`. Its sidecar and obsolete socket volume
were removed; data/publication mounts were preserved.

### Publication environment and end-to-end verification

The single-container supervisor must set the .NET child's user environment as
well as UID/GID. It explicitly sets `HOME`, `USER`, `LOGNAME`, `SHELL`,
`DOTNET_CLI_HOME`, and XDG config/cache/data/state paths from the application's
passwd entry and removes any root-only inherited runtime directory. App/Git
configuration remains available to the app; the bridge still receives only its
minimal whitelist, not GitHub credentials.

An incident on 2026-10-29 left the app running as UID 1000 with `HOME=/root`.
Libgit2 failed with `failed to stat '/root/.gitconfig'`. SSH, model access, and
local collection continued normally while the public dashboard stayed at its
morning reset. Correcting the actual child environment recovered all pending
records without changing the app/bridge UID separation.

Startup now attempts publication after repository recovery and before opening
SSH, so a backlog is not stranded until the next attack. Pushes use normal
refspecs, not forced history updates. Subsequent session-triggered publication
was verified independently of that one-time startup recovery.

Container health alone is **not** an end-to-end publication check. Verify the
runtime publication HEAD against the remote and inspect fresh public JSON:

```bash
docker exec --user test funnypot-container git -C /home/test/app/frontend log -1 --oneline
docker exec --user test funnypot-container git -C /home/test/app/frontend ls-remote origin refs/heads/data
```

For environment diagnosis, inspect only safe fields from the **actual .NET
process's** `/proc/PID/environ`, as that process's UID. A new `docker exec --user
test` process may show the correct HOME even when the supervised application
inherited the wrong one. Never dump the full environment: it can contain GitHub
or other application credentials. Confirm publication from the public data
branch, not merely from local dirty files or an unrelated diagnostic process.

## Optional full Kilo / ChatGPT provider (legacy)

The default remains OpenRouter. Set `LLM_PROVIDER=kilo` to route cache misses
through Kilo's authenticated provider instead; no OpenRouter key is needed and
there is no automatic cross-provider fallback. `KILO_MODEL` defaults to
`openai/gpt-6.1-terra` (separate from the OpenRouter `LLM_MODEL`).

Run a **dedicated** Kilo server as the host user already logged into ChatGPT
through Kilo. Use an empty directory, not this repository or your normal agent
workspace, and install the supplied text-only agent configuration there:

```bash
mkdir -p /tmp/funnypot-kilo
cp config/kilo/kilo.json /tmp/funnypot-kilo/kilo.json
cd /tmp/funnypot-kilo
~/.kilo/bin/kilo serve --pure --hostname 127.0.0.1 --port 4197
```

For a host-run FunnyPot process, use `KILO_BASE_URL=http://127.0.0.1:4197`.
For Docker, Compose supplies `http://host.docker.internal:4197`, but a server
bound to loopback is not reachable from a bridge container. Bind Kilo to a
host interface reachable by that container, restrict port 4197 with the host
firewall to the Docker bridge, and set the same strong `KILO_SERVER_PASSWORD`
on both Kilo and FunnyPot. Never expose the Kilo control API publicly. Basic
authentication is supported, with `KILO_SERVER_USERNAME=kilo` by default;
the password can also be mounted at `/run/secrets/kilo_server_password`
(preferred over a container environment variable).
Use HTTPS if traffic crosses an untrusted network. Do not mount Kilo's auth
store into the honeypot container.

`KILO_DIRECTORY` is an absolute path **on the Kilo server host**, defaulting to
`/tmp/funnypot-kilo`; `KILO_AGENT` defaults to `funnypot-terminal`. Keep the
dedicated directory/config available across restarts (use a persistent path
instead of `/tmp` for production). Run Kilo under a service manager for
unattended operation. `--pure` disables external plugins; use a dedicated
Kilo config without MCP servers or other integrations for this service.

Each call creates a fresh session with all permissions denied and tools
disabled, sends the existing FunnyPot conversation as text data, then aborts
and deletes the session. The adapter limits concurrent requests (default 2,
`llm.kilo.max-concurrent-requests`), applies `llm.timeout-seconds` including
queue time, and rejects tool, failed, and incomplete responses rather than
learning them. Kilo cleanup failures are logged. Subscription quotas and
Kilo's own model output limits still apply; OpenRouter's `max-tokens` setting
is not forwarded to Kilo. Kilo may retain operational logs even after sessions
are deleted. Existing cache hits do not call either provider.

## Response dictionary

### Terra asset rebuild and clean telemetry baseline (2026-10-29)

The active cache was regenerated from scratch with `openai/gpt-6.1-terra`:
272 baseline entries, plus 10 fresh synthetic scenario transcripts containing
47 command/response turns. Existing keys supplied coverage, but no previous
model answers were reused. Canonical placeholders were instantiated with synthetic
TEST-NET endpoints/passwords before model generation and re-templated afterward.

Assets under `FunnyPot/data/`:

- `command_responses.json`: validated generated baseline (`origin: seed`, with
  actual Terra model attribution and generation timestamps). Future runtime
  additions continue to use `origin: llm`.
- `attack_scenarios.json`: explicitly synthetic Terra-generated playbooks and
  stateful simulated transcripts, separate from real attacker telemetry.
- `terra-rebuild-manifest.json` and `terra-rebuild-complete.json`: source-cache
  fingerprint, generation model, completion time, and asset counts.

Before reset, writers were stopped and a private, Git/Docker-ignored backup was
created at `backups/terra-rebuild-20261029/` (about 4.5 GB). It includes the entire
runtime publication worktree and Git database, raw session logs, runtime logs,
the response dictionary, and the separate host frontend checkout. The generation
plan, original input, intermediate responses, validation report, and scenario
checkpoints are preserved in `generation-work.tar.gz` there, not left in the
active data directory. Do not publish this backup: it contains raw harvested data
and repository metadata.

Runtime attack history and threat-intelligence aggregates were cleared after the
backup, then published as data commit `6d996207` using a normal fast-forward push.
Git history was **not** rewritten: prior records remain recoverable in historical
commits and the local backup. Generated scenarios were not injected into real
attacker events. New activity naturally repopulates the clean telemetry baseline.

For a future rebuild:

1. Stop writers and back up the cache, publication volume, and runtime logs.
2. Temporarily set `FUNNYPOT_MAINTENANCE=1` in the host-local Compose override,
   then recreate the container. Only the bridge runs; SSH is disabled. The
   maintenance health probe checks the bridge, not an SSH listener.
3. Generate into a separate staging directory alongside the live cache:

   ```bash
   docker exec --user test funnypot-container dotnet FunnyPot.dll \
     --rebuild-response-assets /var/lib/funnypot/command_responses.json \
     /var/lib/funnypot/terra-rebuild-STAMP
   ```

   This is resumable against the **same original input file** (SHA-256 checked).
   It uses at most two simultaneous model requests, retries invalid output, and
   never executes generated shell commands. Failed generation does not activate
   incomplete assets. Persona checks enforce CPU/GPU/OS expectations, stdout-only
   CPU fallback baseline behavior, expected labels, and full bounded CPU records.
   Binary terminal junk is allowed intentional truncation; normal text is not.
4. After `complete.json` exists, activate and run tests **while still offline**:

   ```bash
   docker exec --user test funnypot-container dotnet FunnyPot.dll \
     --activate-response-assets /var/lib/funnypot/terra-rebuild-STAMP
   dotnet test FunnyPot.sln --no-restore
   ```

5. If also resetting collected history, with its backup already verified:

   ```bash
   docker exec --user test funnypot-container dotnet FunnyPot.dll \
     --reset-attack-history /home/test/app/frontend
   ```

   This clears current events, summaries, threat intelligence, debriefs and raw
   session files, commits the new baseline on the configured data branch, and
   pushes without force. It fails if the remote head changed before reset.
6. Archive/remove staging, remove the maintenance override, rebuild the image
   with the new baseline assets, and recreate the single container. Verify health,
   model access, and persistent data before resuming normal operation.

Reset/generation/activation CLIs reject use without the maintenance flag. Manual
model checks bypass event logging, avoiding synthetic attacks in the clean dataset.

### Earlier Kilo host deployment (superseded on 2026-10-29)

- Kilo runs as the enabled user service `funnypot-kilo.service`, with user
  lingering enabled so the service can start without an interactive login.
- Its isolated agent config and password live under
  `/home/felix/.local/share/funnypot-kilo/`. The password is not tracked in Git.
- The endpoint binds only `172.18.0.1:4197` on the existing Docker bridge,
  not loopback or a public/LAN interface. If the Compose network gateway
  changes, update the service launcher and local override together.
- The ignored `docker-compose.override.yaml` selects `LLM_PROVIDER=kilo`,
  `KILO_MODEL=openai/gpt-6.1-terra`, and mounts the server password as a
  Docker secret. Normal `docker compose up -d` retains these local settings.
- The pre-deploy image is tagged `funnypot:pre-kilo-20261029`; the response
  dictionary backup is `backups/pre-kilo-command-responses-20261029.tar.gz`.
- Deployment used direct Compose commands rather than `scripts/deploy.sh`
  because that script also commits and pushes changes. Git publication is
  handled separately, excluding credentials and host-local configuration.

To roll back the application image and provider without deleting data:

```bash
docker image tag funnypot:pre-kilo-20261029 funnypot-funnypot:latest
LLM_PROVIDER=openrouter docker compose -f docker-compose.yaml up -d --no-build funnypot
```

The explicit Compose file excludes the local Kilo override for this rollback;
subsequent plain Compose commands will select Kilo again unless the override
is removed or changed. Do not restore the response backup unless necessary,
as that discards commands learned since deployment.

### Persistence

- Exact cached commands are loaded from `/var/lib/funnypot/command_responses.json` before the SSH listener starts.
- Successful responses from the selected LLM provider are added to this file atomically at runtime.
- The `command-responses` named volume preserves learned responses across container rebuilds and recreation.
- `docker compose down -v` deletes the learned dictionary and causes the next deployment to initialize it from the image seed.

## Logs

- `docker logs funnypot-container --tail N` — stdout/stderr. Often
  empty during normal operation because the app logs through
  `Logger.LogMsg` to `/var/log/funnypot/` inside the container, not
  stdout.
- `docker exec funnypot-container ls /var/log/funnypot/` — app log
  files.

## Stop / start / restart

```bash
docker compose stop             # graceful, leaves container
docker compose start            # restart with same image
docker compose restart          # stop + start
docker compose down             # remove container (keeps image)
docker compose up -d --build    # rebuild image + recreate container
```

## Common pitfalls

- **"Container is up but no port"** — almost always cold start, not a
  bug. Wait 2–3 minutes and re-check `docker exec cat /proc/1/net/tcp`.
- **"Healthcheck says unhealthy after rebuild"** — see above.
- **"Build fails with submodule error"** — the repo has a `frontend/`
  submodule. Run `git submodule update --init --recursive` first, or
  use `scripts/deploy.sh` which checks this for you.
- **"Don't trust host `ps` for the running app"** — use
  `docker ps` / `docker exec`. The host's `dotnet` processes are
  short-lived `dotnet build` / `dotnet test` invocations or the
  container's PID 1 surfaced through a different namespace.
- **"App reaches `bash: who are you: command not found` for real
  commands"** — the LLM is over-applying the meta-question fallback.
  This is a prompt regression, not a deploy issue. Re-pull the latest
  image and confirm `git rev-parse HEAD` matches the running image.
