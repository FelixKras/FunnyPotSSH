---
id: "mp-20261112-1b08b9796d"
kind: "decision"
title: "Bounded chunked HTTP request decoding for Ollama honeypot"
scope: "project"
status: "approved"
created_at: "2026-11-12T06:02:28.449093Z"
author: "Forge"
confidence: 1.0
approved_at: "2026-11-12T06:02:28.512900Z"
reviewer: "Forge"
source_refs:
  - "FunnyPot/OllamaHoneypot.cs"
  - "FunnyPot.Tests/OllamaHoneypotTests.cs"
  - "docs/DEPLOY.md"
tags:
  - "ollama"
  - "http"
---

# Bounded chunked HTTP request decoding for Ollama honeypot

Implemented user-requested HTTP/1.1 Transfer-Encoding chunked support in OllamaHoneypot. Reassembles bytes before UTF-8 decoding and JSON parsing, captures decoded body, and preserves only actually received bytes on incomplete Content-Length or chunked input (fixing previous zero-padding of incomplete fixed-length bodies). Limits: 16 KiB decoded body, 1024 nonempty chunks, 1 KiB metadata lines, 8 KiB aggregate chunk framing/trailers, existing 10-second total deadline. Ignores bounded extensions; discards trailer values including credentials; rejects ambiguous CL+TE, duplicate TE, HTTP/1.0 chunking, unsupported transfer codings and forbidden framing/routing trailers. Error capture uses fixed descriptive framing reasons and oversize returns 413. Zero-token canned replies, host 11433 and hourly publication unchanged. Tests: 181 .NET tests passed including network roundtrips, fragmented reads, UTF-8 chunk splits, Expect continue, partial capture, cancellation, cumulative limits and malformed framing; frontend checks and Compose validation passed. Deployment pending after this checkpoint.
