---
id: mp-artifact-publication-home-environment-incident-2026-10-29
kind: fact
title: Supervisor user-environment publication outage and verified backlog recovery
scope: project
status: approved-by-request
author: Forge
created_at: 2026-10-29
source_refs:
  - User request to fix publication and record the entire incident and rationale in memory
  - subscription-bridge/supervise.py
  - subscription-bridge/test_supervise.py
  - FunnyPot/Program.cs
  - FunnyPot.Tests/UnitTests.cs
  - docs/DEPLOY.md
  - .memory-plane/artifacts/subscription-bridge-2026-10-29.md
  - .memory-plane/artifacts/terra-assets-and-history-reset-2026-10-29.md
  - Production process-environment, publication-log, and public JSON checks on 2026-10-29
  - Private pending-telemetry backup at backups/publish-home-fix-20261029T190449Z/
  - Publication data commit fb8fac4b2b36f8929f5c9bd956f62570032adb68
  - Subsequent automatic publication data commit 65a64b3e
---

## Context and user decisions

The original OpenRouter transport was first extended with a full Kilo server,
then replaced at the user's request with a minimal subscription bridge calling
OpenAI directly. The user clarified that "inside Docker" meant **inside the same
FunnyPot container**, not a second sidecar. The resulting single-container design
keeps the original app UID 1000 and uses bridge UID 20001. A small root-owned
Python PID 1 supervisor starts both and manages a bridge-private access-token
copy. "Supervisor" in the incident conversation refers to that script, not
another person or AI supervising Forge.

Different Unix users were intentional to protect the access token: same-UID
processes normally cannot be distinguished with file permissions. The app can
connect to the socket but cannot traverse either the protected host-token mount
or the bridge-private credential directory. The refresh token remains on the
host. Switching both processes to the app UID would weaken that isolation and
was not needed to fix publication. The user accepted retaining UID separation.

## Incident and root cause

After the morning Terra asset rebuild/reset, the container was healthy and
collected commands locally but the dashboard's data branch remained at reset
commit `6d996207`. At the 18:49 UTC investigation, local records showed 1,949
command/results, 277 command-bearing sessions, and a latest command at 18:26 UTC;
connection events continued afterward. This was stale publication, not an idle
or stopped honeypot.

Forge's supervisor implementation changed the child UID/GID but did not set its
user environment. The actual .NET process inherited `HOME=/root` from PID 1.
Libgit2 repeatedly failed with `failed to stat '/root/.gitconfig'`, before
publication could commit/push. This was an implementation regression introduced
by Forge, not a user instruction to break publication or a failure of the
separate-UID security design.

Earlier validation checked SSH, model output, token permissions, and container
health but omitted real publication from the supervised process to the public
data branch. A separate `docker exec --user test` probe misleadingly showed
`HOME=/home/test`; inspecting the running .NET process's environment under its
own UID confirmed `HOME=/root`. No complete environment or credential values
were printed or stored in memory.

## Correction

- `application_environment` constructs the app environment from its passwd
  entry: HOME, USER, LOGNAME, SHELL, DOTNET_CLI_HOME, and XDG config/cache/data/state
  paths. An inherited root-only XDG runtime directory is removed. Required app
  publication/provider configuration is preserved; the parent environment is
  not mutated. The bridge's minimal environment whitelist remains separate.
- Regression tests cover inherited root paths, preserved configuration, unchanged
  parent environment, and a real spawned child's HOME/config paths.
- Startup attempts `Logger.PushToGit("startup-recovery")` after repository
  preparation and before SSH starts, recovering pending data even without new
  attackers. Normal publication continues to run on session/event triggers.
- Publication push refspecs no longer include the force prefix. A test checks
  ordinary local/data branch refspecs; non-fast-forward failures cannot silently
  rewrite remote history.
- An existing cache test incorrectly assumed all live-file CPU entries retain
  seed origin. It now accepts legitimate runtime `llm` additions while keeping
  the CPU/persona output assertions intact.

## Backup, deployment, and verified recovery

Before recreation, writers were stopped and pending publication data, global
stats, raw session logs, and runtime logs were copied into the private ignored
backup `backups/publish-home-fix-20261029T190449Z/` (parent mode 0700). No pending
records were discarded to make the deployment appear healthy.

The corrected single container started at 19:09:41 UTC. The actual .NET process
then had HOME `/home/test` and XDG config home `/home/test/.config`. Startup
recovery pushed data commit `fb8fac4b` at 19:09:48 UTC; local and remote HEADs
matched. Public JSON independently showed 8,288 events, 1,949 command results,
531 sessions and 277 command-bearing sessions.

An integrity check compared backed-up event identity tuples and session IDs with
the **public** downloaded data: all 8,288 backed-up events and all 531 backed-up
sessions were present. Only aggregate validation results were printed; no raw
telemetry, identities, passwords, or tokens were stored in memory. A subsequent
natural session produced automatic data commit `65a64b3e` at 19:12:19 UTC,
confirming ongoing publication rather than only a manual one-time recovery.
Further automatic commits followed, with local/remote HEAD matching `57f08cde`
at final verification. No publication failures were logged after the corrected
restart at that check.

All 137 .NET and 19 Python tests passed; Compose and whitespace validation passed.
The container was healthy, the app-user live Terra check returned `Linux`, and
attempts to open both token paths as the app UID still raised PermissionError.
No UID consolidation or authentication-token exposure was required.

## Durable operational rule

Healthy SSH/model transport does not prove a functioning honeypot pipeline.
After process-user, supervisor, container, credential, or publication changes,
validate the **actual supervised process's** identity/environment, a successful
remote data commit, fresh public dashboard JSON, and retained pending records.
Do not substitute `docker exec` defaults for the running process's environment,
or expose full environments while diagnosing credentials. Record earlier
verification gaps explicitly instead of treating a green container as proof of
end-to-end deployment success.
