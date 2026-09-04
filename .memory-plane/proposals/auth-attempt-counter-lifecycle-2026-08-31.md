---
id: mp-proposal-auth-attempt-counter-lifecycle-2026-08-31
kind: claim
title: Authentication Attempt Counter Lifecycle
scope: project
status: proposed
created_at: 2026-08-31
author: OpenCode
confidence: 1.0
source_refs:
  - FunnyPot/Program.cs:48
  - FunnyPot/Program.cs:386-499
  - FunnyPot/Program.cs:321-343
tags:
  - authentication
  - telemetry
---

# Authentication Attempt Counter Lifecycle

`AuthAttemptLogEntry.AttemptNumber` is a process-lifetime count of password attempts from a normalized remote IP, not a per-connection or per-session attempt number. The disconnect cleanup does not remove the IP from `AuthAttempts`.

The harvest threshold is evaluated as `tries >= PasswordHarvestAttempt`. Consequently, the third cumulative password attempt is accepted when the threshold is three, and every subsequent attempt from that IP is also accepted while the process remains running. Telemetry values such as 27 therefore indicate the IP's cumulative attempt ordinal, not that one SSH authentication exchange was rejected 26 times.
