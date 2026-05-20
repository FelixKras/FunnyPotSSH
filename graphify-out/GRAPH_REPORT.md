# Graph Report - FunnyPot  (2026-05-20)

## Corpus Check
- 10 files · ~14,240 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 177 nodes · 299 edges · 18 communities (13 shown, 5 thin omitted)
- Extraction: 100% EXTRACTED · 0% INFERRED · 0% AMBIGUOUS
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `7d16e7ec`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- [[_COMMUNITY_Community 0|Community 0]]
- [[_COMMUNITY_Community 1|Community 1]]
- [[_COMMUNITY_Community 2|Community 2]]
- [[_COMMUNITY_Community 3|Community 3]]
- [[_COMMUNITY_Community 4|Community 4]]
- [[_COMMUNITY_Community 5|Community 5]]
- [[_COMMUNITY_Community 6|Community 6]]
- [[_COMMUNITY_Community 7|Community 7]]
- [[_COMMUNITY_Community 8|Community 8]]
- [[_COMMUNITY_Community 9|Community 9]]
- [[_COMMUNITY_Community 10|Community 10]]
- [[_COMMUNITY_Community 11|Community 11]]

## God Nodes (most connected - your core abstractions)
1. `Logger` - 23 edges
2. `Program` - 20 edges
3. `DataHarvester` - 18 edges
4. `InputValidatorTests` - 12 edges
5. `FakeFileSystem` - 12 edges
6. `StaticResponseStore` - 9 edges
7. `DataHarvesterTests` - 7 edges
8. `LlmRateLimiter` - 6 edges
9. `NtfyNotifier` - 6 edges
10. `InputValidator` - 5 edges

## Surprising Connections (you probably didn't know these)
- `Program` --references--> `int`  [EXTRACTED]
  FunnyPot/Program.cs → FunnyPot/Program.cs  _Bridges community 6 → community 3_
- `Program` --references--> `string`  [EXTRACTED]
  FunnyPot/Program.cs → FunnyPot/Program.cs  _Bridges community 6 → community 0_
- `Program` --references--> `bool`  [EXTRACTED]
  FunnyPot/Program.cs → FunnyPot/Program.cs  _Bridges community 6 → community 10_
- `DataHarvester` --references--> `int`  [EXTRACTED]
  FunnyPot/Program.cs → FunnyPot/Program.cs  _Bridges community 3 → community 4_
- `FakeFileSystem` --references--> `object`  [EXTRACTED]
  FunnyPot/Program.cs → FunnyPot/Program.cs  _Bridges community 0 → community 9_

## Communities (18 total, 5 thin omitted)

### Community 0 - "Community 0"
Cohesion: 0.1
Nodes (19): AuthAttemptLogEntry, ChatMessage, ChatRequestData, CommandLogEntry, CommandResultLogEntry, DhsCommandAnalysis, GlobalStats, HarvestedCredential (+11 more)

### Community 1 - "Community 1"
Cohesion: 0.11
Nodes (5): DataHarvesterTests, LoggerTests, NtfyNotifierTests, ProgramTests, SCPDetectorTests

### Community 3 - "Community 3"
Cohesion: 0.16
Nodes (7): ConcurrentDictionary, CommandResolver, InputValidator, LlmRateLimiter, SCPDetector, int, Regex

### Community 5 - "Community 5"
Cohesion: 0.12
Nodes (15): Asset Value Perception, Data Acquisition Architecture, Data Collection Modules, Data Harvesting Specification: LLM-Driven SSH Analytics, GitHub Pages Publication, Log Format, M-1: SSH Protocol and Authentication Metadata, M-2: Behavioral and Tactical Metrics (+7 more)

### Community 6 - "Community 6"
Cohesion: 0.2
Nodes (5): FieldInfo, Program, ShellSessionAnalytics, HttpClient, SemaphoreSlim

### Community 7 - "Community 7"
Cohesion: 0.23
Nodes (4): DateTime, Logger, Lazy, TimeSpan

### Community 10 - "Community 10"
Cohesion: 0.39
Nodes (3): bool, Dictionary, StaticResponseStore

## Knowledge Gaps
- **33 isolated node(s):** `HttpClient`, `SemaphoreSlim`, `FieldInfo`, `HarvestedCredential`, `AuthAttemptLogEntry` (+28 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **5 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Logger` connect `Community 7` to `Community 0`, `Community 2`?**
  _High betweenness centrality (0.096) - this node is a cross-community bridge._
- **Why does `Program` connect `Community 6` to `Community 0`, `Community 2`, `Community 3`, `Community 4`, `Community 10`?**
  _High betweenness centrality (0.074) - this node is a cross-community bridge._
- **Why does `DataHarvester` connect `Community 4` to `Community 0`, `Community 3`, `Community 7`?**
  _High betweenness centrality (0.064) - this node is a cross-community bridge._
- **What connects `HttpClient`, `SemaphoreSlim`, `FieldInfo` to the rest of the system?**
  _33 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Community 0` be split into smaller, more focused modules?**
  _Cohesion score 0.1 - nodes in this community are weakly interconnected._
- **Should `Community 1` be split into smaller, more focused modules?**
  _Cohesion score 0.11 - nodes in this community are weakly interconnected._
- **Should `Community 5` be split into smaller, more focused modules?**
  _Cohesion score 0.12 - nodes in this community are weakly interconnected._