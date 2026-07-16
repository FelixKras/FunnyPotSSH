---
id: mp-artifact-fake-filesystem-behavior
kind: fact
title: Fake Filesystem Behavior
scope: project
status: approved-by-request
created_at: 2026-07-12
author: Forge
source_refs:
  - FunnyPot/FakeFileSystem.cs
  - FunnyPot/Program.cs
  - FunnyPot.Tests/UnitTests.cs
---

# Fake Filesystem Behavior

FunnyPot maintains a per-shell fake filesystem to make command responses continuous within a session. The filesystem starts at `/home/remote`, is keyed by shell session ID, and is removed when the shell finalizes.

## Path And Directory Model

- `ResolvePath` supports absolute paths, relative paths, `.`, `..`, `~`, quoted paths, and simple shell-noise stripping around redirects and operators.
- Seeded directories cover common Linux roots and attacker-interesting paths under `/etc`, `/home`, `/root`, `/var`, `/tmp`, `/run`, `/bin`, `/usr`, `/sbin`, `/proc`, `/opt`, `/srv`, `/usr/local`, `/var/www`, and `/var/lib`.
- `IsValidDirectory` allows seeded directories plus broad plausible prefixes like `/home`, `/tmp`, `/run`, `/opt`, `/srv`, `/var`, and `/usr/local`.

## File Model

- Seeded files include OS identity files, passwd/group/shadow-like content, SSH config, sudoers, motd, profile/environment files, and fictional high-value files.
- Binary paths under executable and library directories exist but return `null` from `ReadFile`; command handling formats binary `cat` output separately.
- Unseeded plausible files under allowed prefixes return generated synthetic content based on extension.
- Synthetic log files include plausible rotated log and SSH acceptance lines.
- Synthetic scripts, configs, JSON, and generic files return stable-looking maintenance content for a requested path.

## State Changes

- `cd` updates `CurrentDirectory` and can create plausible directory paths.
- `touch`, `mkdir`, `rm`, `rmdir`, `cp`, `mv`, and echo redirection mutate per-session filesystem state.
- Download commands such as `curl`, `wget`, `fetch`, and `tftp` materialize a downloaded payload placeholder at the inferred output path.
- Compound shell commands apply local state changes before LLM or fallback response generation.
- SCP upload commands touch the remote filename in fake filesystem state; actual SCP binary content capture is handled separately by `SCPUploadSession`.

## Continuity Rules

- Files written or downloaded during a shell session can be read later in the same shell session.
- Fake filesystem state is not shared across shell session IDs.
- State is removed on shell finalization through `FakeFileSystem.Remove(sessionId)`.

## Known Limits

- The fake filesystem is an in-memory model, not a full shell or POSIX filesystem.
- Permissions are primarily prompt-driven and response-driven; the fake filesystem itself does not enforce rich UNIX ownership or mode semantics.
- Synthetic content can be generated for broad path prefixes even if no exact file was seeded.
