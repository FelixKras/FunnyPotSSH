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

## Optional Kilo / ChatGPT provider

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

### Current host deployment (2026-10-29)

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
