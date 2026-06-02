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
threads), but JIT compilation of `FunnyPot.dll` plus initial
`FakeFileSystem` / LLM client setup runs before the listener is up.
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

## Where this doc fits in the graph

This file is extracted as a doc node by `graphify` (see
`graphify-out/manifest.json`). The deploy-related code nodes
(`run-docker.sh`, `scripts/deploy.sh`, `docker-compose.yaml`,
`Dockerfile`) are also in the graph. Use:

```bash
graphify query "how is this app deployed"
graphify path "scripts/deploy.sh" "docker-compose.yaml"
```

to navigate from this doc to the concrete deploy artifacts.
