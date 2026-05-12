# Data Harvesting Specification: LLM-Driven SSH Analytics

This document defines the requirements for the Data Harvesting Subsystem (DHS), focused on extracting, processing, correlating, and publishing telemetry from SSH-based LLM simulations for later presentation on a static GitHub Pages site.

## Data Acquisition Architecture

The DHS must operate as a non-blocking ingestion pipeline. It must capture raw SSH/session events, authentication metadata, command behavior, LLM interaction signals, and derived analytics without delaying the attacker session.

## Data Collection Modules

### M-1: SSH Protocol and Authentication Metadata

| ID | Data Point | Requirement |
| --- | --- | --- |
| 1.1 | Fingerprint Hash | Capture key exchange, cipher, and MAC sequences where available. Generate JA3/JA4S-style hashes to identify scanner signatures and client libraries such as LibSSH and Paramiko. |
| 1.2 | Credential Entropy | Log every `user:pass` attempt. Calculate password entropy and Levenshtein distance between sequential attempts to distinguish dictionary spraying from manual mutation. |
| 1.3 | Infrastructure Profiling | Perform source IP lookup against ASN or proxy datasets and categorize traffic as residential proxy, hosting/cloud, VPN/Tor, private, or unknown. |

### M-2: Behavioral and Tactical Metrics

| ID | Data Point | Requirement |
| --- | --- | --- |
| 2.1 | Command Sequence Latency | Measure milliseconds between the previous command completion and next command start. Treat sub-50ms latency as an automation indicator. |
| 2.2 | Discovery Depth Score | Score targeted files by perceived value: `/etc/passwd` = 1, `~/.ssh/` = 5, `/etc/kubernetes/` = 10. Aggregate per session to identify sophisticated actors. |
| 2.3 | Standard Error Ratio | Track total commands and failed commands. A high failure rate indicates blind scripts; a low failure rate indicates adaptive manual probing. |
| 2.4 | Persistence Vector Analysis | Categorize attempted persistence methods as systemd, cron, bash profiles, or SSH `authorized_keys`. |

### M-3: Payload and Egress Telemetry

| ID | Data Point | Requirement |
| --- | --- | --- |
| 3.1 | Binary Acquisition | Extract URLs from `curl`, `wget`, `fetch`, and `tftp` commands. Queue background download and SHA-256 hashing for captured payloads. |
| 3.2 | Egress Profiling | Log simulated outbound socket requests and command-level network targets to identify C2 infrastructure or secondary targets. |
| 3.3 | Tunneling Intent | Log SSH dynamic forwarding (`-D`), local/remote forwarding (`-L`/`-R`), and proxy tools such as `frp`, `chisel`, or `socat`. |

### M-4: LLM Interaction and Persona Metrics

| ID | Data Point | Requirement |
| --- | --- | --- |
| 4.1 | Persona Breakout Attempts | Detect prompt injection patterns such as "ignore previous instructions" and queries about the underlying LLM or infrastructure. |
| 4.2 | Turing Metric | Measure total interaction time and compare with static honeypot baselines to compute simulation multiplier and engagement efficacy. |
| 4.3 | Semantic Drift | Track query complexity over time, from basic discovery to abstract probes, to flag advanced manual actors. |
| 4.4 | Hallucination Feedback | Log interactions with files or services mentioned by the LLM but not part of the canonical simulation model as high-fidelity canary signals. |

## Processing and Correlation

### Temporal Correlation

The DHS must calculate Time-to-Compromise as the delta between initial socket connection and first simulated successful shell login.

### Asset Value Perception

The DHS must assign an AVP score from 0 to 100 based on the intensity and specificity of exfiltration, discovery, persistence, egress, and tunneling attempts.

## Storage and Publication

### Log Format

All harvested events must be emitted as structured JSONL. Human-readable YAML logs may remain as a secondary operational artifact.

### Storage Tiers

Hot tier: append-only JSONL and summary JSON under the runtime log directory and `frontend/data/` for fast presentation.

Cold tier: full raw session transcripts, captured payload metadata, and binaries in object storage or an equivalent archival location.

### Metrics Export

The DHS must expose or generate metrics for active sessions by ASN category, MITRE ATT&CK technique distribution, and mean time of engagement per attacker profile.

### GitHub Pages Publication

The runtime must accumulate presentation data under `frontend/data/harvest.jsonl` and `frontend/data/harvest_summary.json`. When `frontend/` is a Git repository and `GITHUB_TOKEN` plus `GITHUB_USER` are set, the DHS must stage session logs, metrics, summary JSON, and `data/`, commit them, and push to the configured static-site data branch for GitHub Pages presentation.
