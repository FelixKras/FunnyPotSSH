# Graph Report - FunnyPot  (2026-05-20)

## Corpus Check
- 10 files · ~11,941 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 145 nodes · 226 edges · 16 communities (11 shown, 5 thin omitted)
- Extraction: 100% EXTRACTED · 0% INFERRED · 0% AMBIGUOUS
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `b1b3cdc0`
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

## God Nodes (most connected - your core abstractions)
1. `Logger` - 23 edges
2. `Program` - 20 edges
3. `DataHarvester` - 18 edges
4. `InputValidatorTests` - 12 edges
5. `DataHarvesterTests` - 7 edges
6. `NtfyNotifier` - 6 edges
7. `InputValidator` - 5 edges
8. `Data Harvesting Specification: LLM-Driven SSH Analytics` - 5 edges
9. `Data Collection Modules` - 5 edges
10. `Storage and Publication` - 5 edges

## Surprising Connections (you probably didn't know these)
- `Program` --references--> `int`  [EXTRACTED]
  FunnyPot/Program.cs → FunnyPot/Program.cs  _Bridges community 1 → community 8_
- `DataHarvester` --references--> `int`  [EXTRACTED]
  FunnyPot/Program.cs → FunnyPot/Program.cs  _Bridges community 8 → community 3_

## Communities (16 total, 5 thin omitted)

### Community 0 - "Community 0"
Cohesion: 0.17
Nodes (5): DateTime, Logger, Lazy, object, TimeSpan

### Community 1 - "Community 1"
Cohesion: 0.15
Nodes (8): bool, ConcurrentDictionary, FieldInfo, Program, ShellSessionAnalytics, HttpClient, SemaphoreSlim, string

### Community 2 - "Community 2"
Cohesion: 0.11
Nodes (5): DataHarvesterTests, LoggerTests, NtfyNotifierTests, ProgramTests, SCPDetectorTests

### Community 4 - "Community 4"
Cohesion: 0.12
Nodes (15): Asset Value Perception, Data Acquisition Architecture, Data Collection Modules, Data Harvesting Specification: LLM-Driven SSH Analytics, GitHub Pages Publication, Log Format, M-1: SSH Protocol and Authentication Metadata, M-2: Behavioral and Tactical Metrics (+7 more)

### Community 5 - "Community 5"
Cohesion: 0.13
Nodes (14): AuthAttemptLogEntry, ChatMessage, ChatRequestData, CommandLogEntry, CommandResultLogEntry, DhsCommandAnalysis, GlobalStats, HarvestedCredential (+6 more)

## Knowledge Gaps
- **36 isolated node(s):** `HttpClient`, `string`, `ConcurrentDictionary`, `SemaphoreSlim`, `FieldInfo` (+31 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **5 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Logger` connect `Community 0` to `Community 1`, `Community 5`?**
  _High betweenness centrality (0.102) - this node is a cross-community bridge._
- **Why does `Program` connect `Community 1` to `Community 8`, `Community 0`, `Community 3`, `Community 5`?**
  _High betweenness centrality (0.088) - this node is a cross-community bridge._
- **Why does `DataHarvester` connect `Community 3` to `Community 8`, `Community 5`?**
  _High betweenness centrality (0.067) - this node is a cross-community bridge._
- **What connects `HttpClient`, `string`, `ConcurrentDictionary` to the rest of the system?**
  _36 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Community 2` be split into smaller, more focused modules?**
  _Cohesion score 0.11 - nodes in this community are weakly interconnected._
- **Should `Community 4` be split into smaller, more focused modules?**
  _Cohesion score 0.12 - nodes in this community are weakly interconnected._
- **Should `Community 5` be split into smaller, more focused modules?**
  _Cohesion score 0.13 - nodes in this community are weakly interconnected._