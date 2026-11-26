---
id: "mp-20261120-eb726d7bd2"
kind: "episode"
title: "SysMonitorCrew-like staging corroborated for November ELF capture"
scope: "project"
status: "approved"
created_at: "2026-11-20T12:41:15.649265Z"
author: "Forge"
confidence: 0.97
approved_at: "2026-11-20T12:41:15.658575Z"
reviewer: "Forge"
source_refs:
  - "frontend-main commit afdd13f4; retained payload sidecars and SSH events; https://darkfoe.ca/post/19-honeypot-six-weeks-later"
tags:
  - "malware"
  - "frontend"
---

# SysMonitorCrew-like staging corroborated for November ELF capture

Retained late SSH session shows OS/CPU and two NVIDIA inventory queries followed by staging to ~/.sysmonitor. Darkfoe independently documented this combination as SysMonitorCrew. This is a staging-pattern match, not a confirmed actor, infrastructure, payload family, or cryptomining behavior. Earlier session commands were not retained. Frontend commit afdd13f4 links matching hash cards to the revised report.
