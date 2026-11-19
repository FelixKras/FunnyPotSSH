---
id: mp-artifact-scp-upload-handling
kind: fact
title: SCP And Binary Upload Quarantine, Metadata, And Analysis
scope: project
status: approved-by-request
created_at: 2026-07-12
updated_at: 2026-11-19
author: Forge
reviewer: Forge
source_refs:
  - FunnyPot/Program.cs
  - FunnyPot.Tests/UnitTests.cs
  - frontend-main/index.html
---

# SCP And Binary Upload Quarantine, Metadata, And Analysis

FunnyPot detects SCP/SFTP uploads, inline binary/control-byte streams, inline encoded binary drops (`base64 -d`, `\x..`, `xxd -r`), and external payload URLs. Rejected and accepted binary uploads are saved to `${Program.LogDir}/uploads/<session-prefix>/` alongside a sidecar `.meta.json` file containing full session provenance and static payload analysis.

## Detection And Sources

- `SCPDetector.IsSCPCommand` returns true for `scp`, `scp ...`, `SCP`, `sftp`, and `sftp-server` style inputs.
- `SCPDetector.IsSCPUpload` matches SCP commands containing upload flags such as `-t`, `-r`, or `-p` in upload mode.
- `SCPUploadHandler.ContainsBinaryBytes` detects null bytes and non-terminal control bytes on raw SSH channel data before or at newline boundaries.
- `SCPUploadHandler.TryExtractInlineEncodedPayload` decodes inline `base64 -d`, `\x..` hex escapes, and `xxd -r` binary staging commands.
- `DataHarvester.ExtractPayloadUrls` extracts both explicit `http://` / `https://` URLs and schemeless `curl` / `wget` / `fetch` / `lwp-download` targets (e.g. `curl 198.51.100.44:8080/bins/x86_64`), normalizing schemeless endpoints to `http://...`.
- `DataHarvester.CapturePayloadMetadataAsync` fetches those URLs with realistic `curl`/`Wget` User-Agent headers, determines the target filename from `-o` / `-O` / `>` redirection or the URL path, and saves up to 5 MiB of the downloaded payload into `${Program.LogDir}/uploads/<session-prefix>/` as a read-only (`0400`) `.quarantine` artifact with `.meta.json` provenance and full static binary analysis.

## Exec Upload State Machine And Rejection Quarantine

- In `SetupShell`, an exec channel detected as SCP upload creates an `SCPUploadSession` with `BinaryUploadContext` and sends an initial zero-byte ACK.
- `SCPUploadSession` progresses through `Header`, `Content`, `Terminator`, `Done`, and `Rejected` states.
- Invalid headers, oversized headers, oversized transfers (`> 5 MiB`), invalid terminators, and incomplete transfers on channel close (`FlushPending`) are not merely dropped: any received bytes (capped at 5 MiB) and a sidecar `.meta.json` record are written to the uploads directory and logged as `scp_upload_rejected`.
- Inline binary input rejected by `InputValidator` (`Binary content not allowed`) is also preserved in the uploads directory with `.meta.json` and logged as `scp_upload_rejected` (`status: "rejected_saved"`).

## Capture, Sidecar Metadata, And Static Analysis

- Captured and rejected uploads are written under `${Program.LogDir}/uploads/<session-prefix>/<timestamp>_<safe-filename>.quarantine`.
- Non-execution hardening:
  - Every quarantined binary and `.meta.json` sidecar is immediately locked down via `SCPUploadHandler.LockDownQuarantinedFile` to strip all execute and write bits (`UnixFileMode.UserRead` / `0400` `-r--------` on Linux and `FileAttributes.ReadOnly`), while quarantine directories are restricted to `0700` (`drwx------`).
  - Stored filenames are stripped of leading dots and suffixed with `.quarantine` so they cannot match executable script extensions or shell startup dotfiles.
  - FunnyPot's SSH shell (`CommandResolver` / `FakeFileSystem`) is purely emulated in memory and never invokes `Process.Start` on attacker commands or uploaded files.
- Every saved artifact gets a companion `<destPath>.meta.json` (`BinaryUploadMetadataRecord`) containing:
  - `SessionId`, `ShellSessionId`, `ExchangeId`, `CapturedAtUtc`
  - `RemoteEndpoint`, `RemoteIp`, `Username`, `ClientVersion`, `SshBanner`, `DeliveryChannel`, `CommandText`
  - `Source` (`scp`, `scp_invalid_header`, `scp_header_overflow`, `scp_raw_stream`, `ssh_pty_binary`, `ssh_exec_binary`, `inline_base64`, `inline_hex_escape`, `inline_xxd_hex`, `url_payload`)
  - `Status` (`captured`, `rejected_saved`, `too_large_saved`, `too_large`), `RejectionReason`, `ScpHeader`, `ScpPermissions`
  - `OriginalFilename`, `StoredFilePath`, `MetadataFilePath`, `ExpectedBytes`, `CapturedBytes`, `Truncated`, `Sha256`
- `BinaryPayloadAnalyzer.Analyze` performs static classification on every saved payload:
  - File format & magic bytes (`ELF 32/64-bit LSB/MSB`, architecture such as `x86-64`/`ARM`/`MIPS`/`AArch64`/`RISC-V`, ELF binary type, script shebangs, `PE/MZ`, `Mach-O`, `gzip`/`bzip2`/`xz`/`zstd`/`ZIP`/`tar`, shell staging text, or raw binary)
  - Shannon byte entropy and packed/encrypted heuristics (`Entropy >= 7.2` or `UPX!` markers)
  - Printable strings, embedded URLs, IPv4 addresses, and suspicious indicators (`xmrig`, `stratum+`, `/dev/tcp/`, `LD_PRELOAD`, `ptrace`, `UPX`, `authorized_keys`, `crontab`, `systemctl`, `chattr`, `wget`, `curl`, `busybox`).
