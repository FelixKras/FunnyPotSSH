# Graph Report - FunnyPot  (2026-05-20)

## Corpus Check
- 14 files · ~14,923 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 190 nodes · 320 edges · 22 communities (17 shown, 5 thin omitted)
- Extraction: 100% EXTRACTED · 0% INFERRED · 0% AMBIGUOUS
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `66e81276`
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
1. `Program` - 28 edges
2. `Logger` - 24 edges
3. `DataHarvester` - 18 edges
4. `InputValidatorTests` - 12 edges
5. `FakeFileSystem` - 12 edges
6. `StaticResponseStore` - 9 edges
7. `DataHarvesterTests` - 7 edges
8. `object` - 6 edges
9. `LlmRateLimiter` - 6 edges
10. `NtfyNotifier` - 6 edges

## Surprising Connections (you probably didn't know these)
- `Program` --references--> `int`  [EXTRACTED]
  FunnyPot/Program.cs → FunnyPot/Program.cs  _Bridges community 7 → community 3_
- `Program` --references--> `string`  [EXTRACTED]
  FunnyPot/Program.cs → FunnyPot/Program.cs  _Bridges community 7 → community 1_
- `Program` --references--> `bool`  [EXTRACTED]
  FunnyPot/Program.cs → FunnyPot/Program.cs  _Bridges community 7 → community 9_
- `DataHarvester` --references--> `int`  [EXTRACTED]
  FunnyPot/Program.cs → FunnyPot/Program.cs  _Bridges community 3 → community 4_
- `FakeFileSystem` --references--> `object`  [EXTRACTED]
  FunnyPot/Program.cs → FunnyPot/Program.cs  _Bridges community 1 → community 8_

## Communities (22 total, 5 thin omitted)

### Community 0 - "Community 0"
Cohesion: 0.06
Nodes (6): DataHarvesterTests, InputValidatorTests, LoggerTests, NtfyNotifierTests, ProgramTests, SCPDetectorTests

### Community 1 - "Community 1"
Cohesion: 0.09
Nodes (20): AuthAttemptLogEntry, BannerSlot, ChatMessage, ChatRequestData, CommandLogEntry, CommandResultLogEntry, DhsCommandAnalysis, GlobalStats (+12 more)

### Community 2 - "Community 2"
Cohesion: 0.2
Nodes (4): DateTime, Logger, Lazy, TimeSpan

### Community 3 - "Community 3"
Cohesion: 0.14
Nodes (7): ConcurrentDictionary, CommandResolver, InputValidator, LlmRateLimiter, SCPDetector, int, Regex

### Community 6 - "Community 6"
Cohesion: 0.12
Nodes (15): Asset Value Perception, Data Acquisition Architecture, Data Collection Modules, Data Harvesting Specification: LLM-Driven SSH Analytics, GitHub Pages Publication, Log Format, M-1: SSH Protocol and Authentication Metadata, M-2: Behavioral and Tactical Metrics (+7 more)

### Community 7 - "Community 7"
Cohesion: 0.22
Nodes (6): FieldInfo, Program, HttpClient, List, SemaphoreSlim, SshServer

### Community 9 - "Community 9"
Cohesion: 0.39
Nodes (3): bool, Dictionary, StaticResponseStore

## Knowledge Gaps
- **36 isolated node(s):** `HttpClient`, `SemaphoreSlim`, `FieldInfo`, `List`, `SshServer` (+31 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **5 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Program` connect `Community 7` to `Community 1`, `Community 2`, `Community 3`, `Community 4`, `Community 5`, `Community 9`?**
  _High betweenness centrality (0.104) - this node is a cross-community bridge._
- **Why does `Logger` connect `Community 2` to `Community 1`, `Community 5`?**
  _High betweenness centrality (0.091) - this node is a cross-community bridge._
- **Why does `DataHarvester` connect `Community 4` to `Community 1`, `Community 3`, `Community 5`?**
  _High betweenness centrality (0.058) - this node is a cross-community bridge._
- **What connects `HttpClient`, `SemaphoreSlim`, `FieldInfo` to the rest of the system?**
  _36 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Community 0` be split into smaller, more focused modules?**
  _Cohesion score 0.06 - nodes in this community are weakly interconnected._
- **Should `Community 1` be split into smaller, more focused modules?**
  _Cohesion score 0.09 - nodes in this community are weakly interconnected._
- **Should `Community 3` be split into smaller, more focused modules?**
  _Cohesion score 0.14 - nodes in this community are weakly interconnected._