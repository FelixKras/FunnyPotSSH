# Graph Report - FunnyPot  (2026-05-20)

## Corpus Check
- 10 files · ~14,479 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 181 nodes · 305 edges · 17 communities (14 shown, 3 thin omitted)
- Extraction: 100% EXTRACTED · 0% INFERRED · 0% AMBIGUOUS
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `88985004`
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

## God Nodes (most connected - your core abstractions)
1. `Program` - 23 edges
2. `Logger` - 23 edges
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
  FunnyPot/Program.cs → FunnyPot/Program.cs  _Bridges community 1 → community 7_
- `Program` --references--> `string`  [EXTRACTED]
  FunnyPot/Program.cs → FunnyPot/Program.cs  _Bridges community 1 → community 2_
- `Program` --references--> `bool`  [EXTRACTED]
  FunnyPot/Program.cs → FunnyPot/Program.cs  _Bridges community 1 → community 8_
- `DataHarvester` --references--> `int`  [EXTRACTED]
  FunnyPot/Program.cs → FunnyPot/Program.cs  _Bridges community 7 → community 5_
- `DataHarvester` --references--> `Regex`  [EXTRACTED]
  FunnyPot/Program.cs → FunnyPot/Program.cs  _Bridges community 4 → community 5_

## Communities (17 total, 3 thin omitted)

### Community 0 - "Community 0"
Cohesion: 0.06
Nodes (6): DataHarvesterTests, InputValidatorTests, LoggerTests, NtfyNotifierTests, ProgramTests, SCPDetectorTests

### Community 1 - "Community 1"
Cohesion: 0.14
Nodes (6): FieldInfo, Program, ShellSessionAnalytics, HttpClient, List, SemaphoreSlim

### Community 2 - "Community 2"
Cohesion: 0.09
Nodes (20): AuthAttemptLogEntry, BannerSlot, ChatMessage, ChatRequestData, CommandLogEntry, CommandResultLogEntry, DhsCommandAnalysis, GlobalStats (+12 more)

### Community 3 - "Community 3"
Cohesion: 0.18
Nodes (4): DateTime, Logger, Lazy, TimeSpan

### Community 4 - "Community 4"
Cohesion: 0.17
Nodes (4): CommandResolver, FakeFileSystem, SCPDetector, Regex

### Community 6 - "Community 6"
Cohesion: 0.12
Nodes (15): Asset Value Perception, Data Acquisition Architecture, Data Collection Modules, Data Harvesting Specification: LLM-Driven SSH Analytics, GitHub Pages Publication, Log Format, M-1: SSH Protocol and Authentication Metadata, M-2: Behavioral and Tactical Metrics (+7 more)

### Community 7 - "Community 7"
Cohesion: 0.24
Nodes (4): ConcurrentDictionary, InputValidator, LlmRateLimiter, int

### Community 8 - "Community 8"
Cohesion: 0.39
Nodes (3): bool, Dictionary, StaticResponseStore

## Knowledge Gaps
- **35 isolated node(s):** `HttpClient`, `SemaphoreSlim`, `FieldInfo`, `List`, `BannerSlot` (+30 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **3 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Logger` connect `Community 3` to `Community 1`, `Community 2`?**
  _High betweenness centrality (0.094) - this node is a cross-community bridge._
- **Why does `Program` connect `Community 1` to `Community 2`, `Community 3`, `Community 4`, `Community 5`, `Community 7`, `Community 8`?**
  _High betweenness centrality (0.089) - this node is a cross-community bridge._
- **Why does `DataHarvester` connect `Community 5` to `Community 2`, `Community 3`, `Community 4`, `Community 7`?**
  _High betweenness centrality (0.063) - this node is a cross-community bridge._
- **What connects `HttpClient`, `SemaphoreSlim`, `FieldInfo` to the rest of the system?**
  _35 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Community 0` be split into smaller, more focused modules?**
  _Cohesion score 0.06 - nodes in this community are weakly interconnected._
- **Should `Community 1` be split into smaller, more focused modules?**
  _Cohesion score 0.14 - nodes in this community are weakly interconnected._
- **Should `Community 2` be split into smaller, more focused modules?**
  _Cohesion score 0.09 - nodes in this community are weakly interconnected._