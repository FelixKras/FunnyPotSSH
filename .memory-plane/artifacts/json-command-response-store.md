---
id: mp-artifact-json-command-response-store
kind: decision
title: JSON Command Response Store
scope: project
status: approved-by-request
created_at: 2026-08-06
author: OpenCode
updated_at: 2026-10-16
reviewer: Kilo
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
- Redis, SQL, NoSQL, telemetry reconstruction, general built-in shell answers, frequent-command rules, and LLM rate-limit fallback responses are not part of response selection. The bounded exploration-chain exception below composes only reviewed seed answers.

## Persistence and Host Bind Mount

- The command dictionary is consolidated in the git-tracked file `FunnyPot/data/command_responses.json`.
- In Compose, `./FunnyPot/data` is bind-mounted directly to `/var/lib/funnypot` using `bind.propagation: rprivate` and `create_host_path: false`.
- Security and egress containment:
  - The bind mount is strictly scoped to the isolated data subdirectory containing only JSON dictionary files; the parent source code, binaries, and system paths are never exposed.
  - The container drops all Linux capabilities (`cap_drop: ALL`) and enforces `no-new-privileges:true`.
  - The container service user runs as non-root `UID=1000:GID=1000` matching host file ownership, preventing privilege escalations while allowing atomic temporary file replacements (`File.Move`).
  - Honeypot attackers are restricted to in-memory `FakeFileSystem` and have zero access or awareness of `/var/lib/funnypot`.
- Runtime updates write to a same-directory temporary file, flush it, and atomically replace `command_responses.json` directly in the repository directory, making learned commands immediately tracked in git.

## Migration

- The former `ssh_responses.jsonl` and code-generated dynamic responses were retired.
- Reviewed, non-dynamic JSONL entries were migrated into 135 exact seed entries.
- The reviewed `chattr`/`lockr` command variants were retained as JSON seed entries rather than code rules.

## Verification

- Tests cover exact matching, empty responses, durable learning, concurrent updates, cache-hit LLM bypass, miss learning, failure non-learning, compound repair, SCP protocol handling, and seed-file validity.
- A live uncached `uname -r` SSH command was answered by `openai/gpt-5.6-luna`, persisted with `origin: llm`, served unchanged on the next request, and remained present after forced container recreation.

## Bounded Exploration Chain Exception (2026-09-25)

At the owner's request, whole-command exact lookup still runs first, but an uncached semicolon chain can concatenate reviewed `seed` responses for stable, state-independent exploration probes and literal quoted `echo` labels. It never invokes a model for individual segments and never learns the composed answer. Pipes, conditionals, substitution, redirects, unbalanced quotes, dynamic paths, and unknown commands fall back to the existing whole-command LLM route. Chains over 32 commands return a terminal-buffer error instead of attempting a large deterministic simulation. This is a narrow exception to the earlier no-local-response rule, not a general shell interpreter (`FunnyPot/Program.cs:2191-2310`, `FunnyPot.Tests/UnitTests.cs:525-603`).

## Frequency-Guided Persona Review (2026-09-25)

The retained event stream between 2026-09-24 20:39 and 2026-09-25 06:20 UTC contained 171 paired cache hits. An exact host-fingerprinting script accounted for 57; the remaining most common keys were password-bearing variants of one `sudo`/CPU-count probe (2-4 hits per variant). This short, rotation-limited window is not a lifetime ranking. The fingerprint output and neighboring OS, kernel, GPU, memory, and disk probes were reviewed against a custom OmegaBlack Debian-derived appliance profile with a vendor 5.15 kernel, Xeon Platinum vCPUs, A100 accelerators, 256 GiB memory, and NVMe storage. Password-bearing keys and raw attacker data are deliberately not recorded in this artifact. Older learned entries elsewhere in the dictionary may still contradict this profile; review them before treating the entire cache as coherent (`FunnyPot/data/command_responses.json`, `FunnyPot/Program.cs`, `FunnyPot/FakeFileSystem.cs`, `/var/log/funnypot/events.jsonl` in the running container).

Some historical learned entries were model refusals, including a refusal to simulate commands. On 2026-09-25, 28 refusal entries and 71 incomplete `sudo -S ... || nproc` replies were removed from the persisted JSON dictionary. `CommandResponseStore.Load` again loads exact entries without silently dropping them; `TryLearnAsync` rejects new refusals and incomplete sudo fallbacks, and `CommandResolver` requests one corrective LLM reply before reporting a transient shell failure. Exact cache hits return the stored response without an intercept or rewrite (`FunnyPot/CommandResponseStore.cs`, `FunnyPot/Program.cs`, `FunnyPot.Tests/UnitTests.cs`, `FunnyPot/data/command_responses.json`).

The deleted `sudo` entries no longer claim a password was absent after stdin supplied one, or invent three attempts for one candidate. These exact commands now miss and go to the LLM. The model prompt forbids phantom three-attempt failures and requires preserving `||` control flow; a fresh incomplete result receives one corrective retry and is not learned. This is not a general sudo interpreter; the next response is model-dependent until a valid one is learned (`FunnyPot/CommandResponseStore.cs`, `FunnyPot/Program.cs`, `FunnyPot.Tests/UnitTests.cs`).

At the owner's request on 2026-09-25, all 66 retained CPU-count-only `nproc` probes were edited to return `96`; another 99 CPU-only exact keys previously deleted for bad responses were restored from the committed dictionary with curated `96` answers. These 165 entries are stored, not intercepted at runtime, and report `origin: seed` rather than attributing the edited answer to an old model. Three compound hardware-fingerprint entries report 96 logical processors; `/proc/cpuinfo` and two direct processor-count probes were aligned. The persona is a dual-socket Xeon Platinum 8259CL host with 48 threads per socket; the prompt instructs the LLM to return 96 for future uncached `nproc` probes, although new LLM output remains probabilistic. Cache keys containing attacker-supplied password candidates are not reproduced in memory artifacts (`FunnyPot/data/command_responses.json`, `FunnyPot/Program.cs`, `FunnyPot/FakeFileSystem.cs`, `FunnyPot.Tests/UnitTests.cs`).

## Canonical Template Matching and Segmented Compound Resolution (2026-10-16)

- `CommandCanonicalizer` normalizes single-token variants in compound commands (such as `echo '<pass>' | sudo -S`, `printf ... | passwd`, URL/IP:port endpoints, `/dev/tcp/<host>/<port>`, random `/tmp|/var/tmp|/dev/shm` filenames, random echo canary tokens, `head -c <bytes>`, sleep durations, and base64 blobs) into indexed placeholders (`{{SUDO_PASS_1}}`, `{{TMP_FILE_1}}`, `{{HOST_1}}`, `{{CANARY_1}}`, `{{HEAD_BYTES_1}}`, etc.).
- `CommandResponseStore` builds an in-memory canonical template index on load and hydrates matched responses with the attacker's concrete values on lookup. Newly learned LLM responses for canonicalizable commands are stored in canonical template form so future single-token variants hit the cache without calling OpenRouter.
- `CommandResolver.TryResolveCompoundChainAsync` splits compound chains across `;`, `&&`, `||`, `&`, and newlines while respecting quotes and subshells, evaluates `&&`/`||` control-flow short-circuiting, strips `/dev/null` redirections (`>/dev/null`, `2>/dev/null`), tracks `VAR=$(...)` assignments across steps in any order, and resolves individual sub-commands from cache/built-ins before falling back to per-step LLM learning (`compound cache` / `compound hybrid`).
- `FunnyPot/data/command_responses.json` was refactored using `CommandCanonicalizer` and local Unsloth Qwen 3.8 (`donghanasd/Huihui-Qwen3.8-27B-abliterated-KO-Ridge-3.7bpw-GGUF` with reasoning disabled), collapsing 184 redundant variant entries (including 163 password-variant `sudo -S ... nproc` keys, dropper chains, `passwd` rotation chains, `head -c` probes, and echo canaries) into canonical `seed` templates, extracting standalone atomic sub-commands (`which curl`, `which df`, `which free`, `which lscpu`, `nvidia-smi -q` pipes), and repairing persona-mismatched outputs (`id -un` -> `remote`, `cat /proc` -> `cat: /proc: Is a directory`), reducing total dictionary entries from 420 to 236.
