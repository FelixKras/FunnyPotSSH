---
id: mp-proposal-auth-attempt-counter-lifecycle-2026-08-31
kind: claim
title: Authentication Attempt Counter Lifecycle
scope: project
status: approved
created_at: 2026-08-31
author: OpenCode
reviewer: Kilo
reviewed_at: 2026-09-25
confidence: 0.98
source_refs:
  - FunnyPot/Program.cs:48-49
  - FunnyPot/Program.cs:445-535
tags:
  - authentication
  - telemetry
---

# Authentication Attempt Counter Lifecycle

`AuthAttemptLogEntry.ConnectionAttemptNumber` is a process-lifetime count of password attempts from a normalized remote IP, not a per-connection or per-session attempt number. Disconnect cleanup does not remove the IP from `AuthAttempts`; the bounded dictionary can evict entries when it exceeds 10,000 tracked remote keys.

The harvest threshold is evaluated as `tries >= PasswordHarvestAttempt`. Consequently, the third cumulative password attempt is accepted when the threshold is three, and subsequent attempts from that IP also qualify until process restart or dictionary eviction. Telemetry values such as 27 indicate the IP's cumulative attempt ordinal, not that one SSH authentication exchange was rejected 26 times.
