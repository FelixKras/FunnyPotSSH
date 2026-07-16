---
id: mp-artifact-scp-upload-handling
kind: fact
title: SCP Upload Handling
scope: project
status: approved-by-request
created_at: 2026-07-12
author: Forge
source_refs:
  - FunnyPot/Program.cs
  - FunnyPot.Tests/UnitTests.cs
  - frontend-main/index.html
---

# SCP Upload Handling

FunnyPot detects SCP and SFTP commands and has a dedicated SCP upload state machine for exec channels opened with an SCP upload command.

## Detection

- `SCPDetector.IsSCPCommand` returns true for `scp`, `scp ...`, `SCP`, `sftp`, and `sftp-server` style inputs.
- `SCPDetector.IsSCPUpload` matches SCP commands containing upload flags such as `-t`, `-r`, or `-p` in upload mode.
- `SCPDetector.ParseSCPUpload` extracts a fallback filename from the command where possible.

## Interactive Command Behavior

- In `CommandResolver.ResolveCommandAsync`, SCP commands return empty output and are marked as static/local handling rather than LLM work.
- SCP upload commands touch the remote filename in the fake filesystem so later filesystem interactions can appear continuous.

## Exec Upload State Machine

- In `SetupShell`, an exec channel detected as SCP upload creates an `SCPUploadSession` and sends an initial zero-byte ACK.
- `SCPUploadSession` progresses through `Header`, `Content`, `Terminator`, `Done`, and `Rejected` states.
- Headers must be SCP `C...` headers and are capped at 4096 bytes.
- Announced file size must be non-negative and no larger than `SCPUploadHandler.MaxUploadBytes`.
- The terminator byte must be zero.

## Capture And Storage

- The maximum accepted upload size is 5 MiB.
- Captured uploads are written under `${Program.LogDir}/uploads/<session-prefix>/`.
- Filenames are sanitized to alphanumeric, dot, underscore, and hyphen characters.
- Stored filenames are prefixed with a UTC timestamp.
- Captured uploads log `scp_upload_captured` with timestamp, session key, filename, byte count, SHA-256, path, and status.
- Rejected oversized uploads log `scp_upload_rejected` with status `too_large`.

## Close Reasons

- Successful upload close reason is `SCPUploadCaptured`.
- Oversized upload close reason is `SCPUploadTooLarge`.
- Invalid header, header-too-long, and invalid terminator paths have dedicated close reasons.

## Verified Behaviors

- Unit tests verify binary upload capture, ACK behavior, path placement under log uploads, payload byte preservation, and rejection of files larger than 5 MiB.
- Dashboard code counts captured and rejected uploads from `scp_upload_captured` and `scp_upload_rejected` events.
